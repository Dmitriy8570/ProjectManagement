using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Employees.Queries;
using BusinessLogic.Identity;
using BusinessLogic.Projects.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Web.ViewModels.Employees;

namespace ProjectManagement.Web.Controllers;

[Route("employees")]
[Authorize]
// Per spec, only the Director manages the employee directory. Detail
// stays open to any authenticated user so project-detail pages can link
// to a person's profile without 403-ing for non-directors. Search stays
// open to PM as well — it powers the project wizard's autocomplete.
public sealed class EmployeesController : Controller
{
    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("")]
    [Authorize(Roles = Roles.Director)]
    public async Task<IActionResult> Index([FromQuery] string? term, CancellationToken ct)
    {
        var employees = await _mediator.Send(
            new SearchEmployeesQuery { Term = term, Limit = 100 }, ct);
        ViewBag.Term = term;
        return View(employees);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        try
        {
            var employeeTask = _mediator.Send(new GetEmployeeByIdQuery { Id = id }, ct);
            var projectsTask = _mediator.Send(new GetEmployeeProjectsQuery { EmployeeId = id }, ct);

            await Task.WhenAll(employeeTask, projectsTask);

            ViewBag.EmployeeProjects = projectsTask.Result;
            return View(employeeTask.Result);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("create")]
    [Authorize(Roles = Roles.Director)]
    public IActionResult Create() => View(new CreateEmployeeViewModel());

    [HttpPost("create")]
    [Authorize(Roles = Roles.Director)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEmployeeViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var command = new CreateEmployeeCommand
            {
                Data = new CreateEmployeeRequest
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Patronymic = model.Patronymic,
                    Email = model.Email,
                    Password = model.Password,
                    Role = model.Role
                }
            };
            var result = await _mediator.Send(command, ct);
            TempData["Success"] = $"{model.LastName} {model.FirstName} was added successfully.";
            return RedirectToAction(nameof(Detail), new { id = result.Id });
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }

        return View(model);
    }

    [HttpGet("{id:int}/edit")]
    [Authorize(Roles = Roles.Director)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        try
        {
            var employee = await _mediator.Send(new GetEmployeeByIdQuery { Id = id }, ct);
            return View(new EmployeeFormViewModel
            {
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Patronymic = employee.Patronymic,
                Email = employee.Email
            });
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:int}/edit")]
    [Authorize(Roles = Roles.Director)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EmployeeFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var command = new EditEmployeeCommand
            {
                Id = id,
                Data = new EditEmployeeRequest
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Patronymic = model.Patronymic,
                    Email = model.Email
                }
            };
            await _mediator.Send(command, ct);
            TempData["Success"] = "Employee updated successfully.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost("{id:int}/delete")]
    [Authorize(Roles = Roles.Director)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteEmployeeCommand { Id = id }, ct);
            TempData["Success"] = "Employee deleted.";
        }
        catch (DomainValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (EntityNotFoundException)
        {
            TempData["Error"] = "Employee not found.";
        }
        return RedirectToAction(nameof(Index));
    }

    // AJAX endpoint for autocomplete in the wizard and edit forms.
    // The optional `roles` parameter (comma-separated) lets the PM picker
    // restrict the dropdown to users eligible to manage a project — only
    // tokens that match a known role are passed through, anything else is
    // silently dropped so a hand-crafted URL can't probe role names.
    [HttpGet("search")]
    [Authorize(Roles = Roles.Director + "," + Roles.ProjectManager)]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] int limit = 10,
        [FromQuery] string? roles = null,
        CancellationToken ct = default)
    {
        var roleFilter = ParseRoles(roles);

        var employees = await _mediator.Send(
            new SearchEmployeesQuery { Term = term, Limit = limit, Roles = roleFilter }, ct);

        return Json(employees.Select(e => new { e.Id, e.FullName }));
    }

    private static IReadOnlyList<string>? ParseRoles(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return null;

        var filtered = csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(r => Roles.AllList.Contains(r))
            .ToArray();

        return filtered.Length == 0 ? null : filtered;
    }
}
