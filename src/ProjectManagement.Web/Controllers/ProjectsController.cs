using BusinessLogic.Common;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using BusinessLogic.Projects.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Web.ViewModels.Projects;

namespace ProjectManagement.Web.Controllers;

[Route("projects")]
public class ProjectsController : Controller
{
    private readonly IMediator _mediator;

    public ProjectsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("")]
    [Route("~/")]
    public async Task<IActionResult> Index([FromQuery] ProjectListViewModel vm, CancellationToken ct)
    {
        vm.Page = vm.Page < 1 ? 1 : vm.Page;
        vm.PageSize = vm.PageSize is < 1 or > 100 ? 10 : vm.PageSize;

        var filter = new ProjectListFilter
        {
            StartDateFrom = vm.StartDateFrom,
            StartDateTo = vm.StartDateTo,
            MinPriority = vm.MinPriority,
            MaxPriority = vm.MaxPriority,
            SortBy = vm.SortBy,
            Descending = vm.Descending,
            Page = vm.Page,
            PageSize = vm.PageSize
        };

        var result = await _mediator.Send(new GetProjectsQuery { Filter = filter }, ct);
        vm.Items = result.Items;
        vm.TotalCount = result.TotalCount;
        return View(vm);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = id }, ct);
            return View(project);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View(new CreateProjectViewModel
        {
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddMonths(3)
        });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProjectViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var command = new CreateProjectCommand
            {
                Data = new CreateProjectRequest
                {
                    Name = model.Name,
                    CustomerCompany = model.CustomerCompany,
                    ExecutingCompany = model.ExecutingCompany,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    ProjectManagerId = model.ProjectManagerId,
                    EmployeeIds = model.EmployeeIds,
                    Priority = model.Priority
                }
            };
            var result = await _mediator.Send(command, ct);
            TempData["Success"] = $"Project \"{model.Name}\" was created successfully.";
            return RedirectToAction(nameof(Detail), new { id = result.Id });
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }
        catch (EntityNotFoundException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }

        return View(model);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = id }, ct);
            return View(new EditProjectViewModel
            {
                Id = id,
                Name = project.Name,
                CustomerCompany = project.CustomerCompany,
                ExecutingCompany = project.ExecutingCompany,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Priority = project.Priority,
                ProjectManagerId = project.ProjectManager.Id,
                ProjectManagerName = project.ProjectManager.FullName
            });
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditProjectViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var command = new EditProjectCommand
            {
                Id = id,
                Data = new EditProjectRequest
                {
                    Name = model.Name,
                    CustomerCompany = model.CustomerCompany,
                    ExecutingCompany = model.ExecutingCompany,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    ProjectManagerId = model.ProjectManagerId,
                    Priority = model.Priority
                }
            };
            await _mediator.Send(command, ct);
            TempData["Success"] = "Project updated successfully.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }
        catch (EntityNotFoundException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }

        return View(model);
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteProjectCommand { Id = id }, ct);
            TempData["Success"] = "Project deleted.";
        }
        catch (EntityNotFoundException)
        {
            TempData["Error"] = "Project not found.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, int employeeId, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new AssignEmployeeToProjectCommand
            {
                Data = new AssignEmployeeToProjectRequest { ProjectId = id, EmployeeId = employeeId }
            }, ct);
            TempData["Success"] = "Employee assigned to project.";
        }
        catch (DomainValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (EntityNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/unassign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unassign(int id, int employeeId, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new UnassignEmployeeFromProjectCommand
            {
                Data = new UnassignEmployeeFromProjectRequest { ProjectId = id, EmployeeId = employeeId }
            }, ct);
            TempData["Success"] = "Employee removed from project.";
        }
        catch (DomainValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (EntityNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Detail), new { id });
    }
}
