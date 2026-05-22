using BusinessLogic.Common;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Queries;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Commands;
using BusinessLogic.Tasks.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Web.ViewModels.Tasks;

namespace ProjectManagement.Web.Controllers;

[Route("tasks")]
public class TasksController : Controller
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator) => _mediator = mediator;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] TaskListViewModel vm, CancellationToken ct)
    {
        vm.Page = vm.Page < 1 ? 1 : vm.Page;
        vm.PageSize = vm.PageSize is < 1 or > 100 ? 10 : vm.PageSize;

        var filter = new ProjectTaskListFilter
        {
            ProjectId   = vm.ProjectId,
            NameSearch  = vm.NameSearch,
            Status      = vm.Status,
            MinPriority = vm.MinPriority,
            MaxPriority = vm.MaxPriority,
            SortBy      = vm.SortBy,
            Descending  = vm.Descending,
            Page        = vm.Page,
            PageSize    = vm.PageSize
        };

        var result = await _mediator.Send(new GetProjectTasksQuery { Filter = filter }, ct);
        vm.Items = result.Items;
        vm.TotalCount = result.TotalCount;

        // Full project record is loaded so the context banner can show dates
        // and manager. ProjectName is kept as a separate flat field for the
        // breadcrumb/heading so views don't need to null-check Project there.
        if (vm.ProjectId.HasValue)
        {
            try
            {
                var project = await _mediator.Send(new GetProjectByIdQuery { Id = vm.ProjectId.Value }, ct);
                vm.Project = project;
                vm.ProjectName = project.Name;
            }
            catch (EntityNotFoundException) { /* fall through — the empty list will show */ }
        }

        return View(vm);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        try
        {
            var task = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);
            return View(task);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create([FromQuery] int? projectId, CancellationToken ct)
    {
        var vm = new CreateTaskViewModel();
        await PopulateCreateLookups(vm, projectId, ct);
        if (projectId.HasValue)
        {
            vm.ProjectId = projectId.Value;
            vm.ProjectLocked = true;
        }
        return View(vm);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTaskViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCreateLookups(model, model.ProjectId, ct);
            return View(model);
        }

        try
        {
            var command = new CreateProjectTaskCommand
            {
                Data = new CreateProjectTaskRequest
                {
                    Name       = model.Name,
                    Comment    = model.Comment,
                    Priority   = model.Priority,
                    Status     = model.Status,
                    ProjectId  = model.ProjectId,
                    AuthorId   = model.AuthorId,
                    AssigneeId = model.AssigneeId
                }
            };
            var result = await _mediator.Send(command, ct);
            TempData["Success"] = $"Task \"{model.Name}\" was created.";
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

        await PopulateCreateLookups(model, model.ProjectId, ct);
        return View(model);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        try
        {
            var task = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = task.ProjectId }, ct);
            return View(new EditTaskViewModel
            {
                Id          = task.Id,
                Name        = task.Name,
                Comment     = task.Comment,
                Priority    = task.Priority,
                Status      = task.Status,
                AssigneeId  = task.Assignee.Id,
                ProjectId   = task.ProjectId,
                ProjectName = task.ProjectName,
                Project     = project
            });
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditTaskViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateEditLookups(model, ct);
            return View(model);
        }

        try
        {
            var command = new EditProjectTaskCommand
            {
                Id = id,
                Data = new EditProjectTaskRequest
                {
                    Name       = model.Name,
                    Comment    = model.Comment,
                    Priority   = model.Priority,
                    Status     = model.Status,
                    AssigneeId = model.AssigneeId
                }
            };
            await _mediator.Send(command, ct);
            TempData["Success"] = "Task updated.";
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

        await PopulateEditLookups(model, ct);
        return View(model);
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteProjectTaskCommand { Id = id }, ct);
            TempData["Success"] = "Task deleted.";
        }
        catch (EntityNotFoundException)
        {
            TempData["Error"] = "Task not found.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, ProjectTaskStatus status, string? returnUrl, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new ChangeProjectTaskStatusCommand { Id = id, Status = status }, ct);
            TempData["Success"] = "Status updated.";
        }
        catch (EntityNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }

        // Url.IsLocalUrl prevents open-redirect: only paths inside this app are honored.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Detail), new { id });
    }

    // Small JSON endpoint used by the Create view to refresh the assignee
    // dropdown when the user changes the project — keeps the page from having
    // to round-trip a full server render.
    [HttpGet("project-members")]
    public async Task<IActionResult> ProjectMembers([FromQuery] int projectId, CancellationToken ct)
    {
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = projectId }, ct);
            var members = new List<object> { new { id = project.ProjectManager.Id, fullName = project.ProjectManager.FullName } };
            members.AddRange(project.Employees.Select(e => new { id = e.Id, fullName = e.FullName }));
            return Json(members);
        }
        catch (EntityNotFoundException)
        {
            return Json(Array.Empty<object>());
        }
    }

    private async Task PopulateCreateLookups(CreateTaskViewModel vm, int? projectId, CancellationToken ct)
    {
        // Pull a generous page of projects (matches the Vue client's behavior).
        var projectsPage = await _mediator.Send(
            new GetProjectsQuery { Filter = new ProjectListFilter { PageSize = 100, SortBy = ProjectSortBy.Name } }, ct);
        vm.AvailableProjects = projectsPage.Items;

        if (projectId.HasValue)
        {
            try
            {
                vm.SelectedProject = await _mediator.Send(new GetProjectByIdQuery { Id = projectId.Value }, ct);
            }
            catch (EntityNotFoundException) { /* selector renders empty; user picks a different project */ }
        }
    }

    private async Task PopulateEditLookups(EditTaskViewModel vm, CancellationToken ct)
    {
        try
        {
            vm.Project = await _mediator.Send(new GetProjectByIdQuery { Id = vm.ProjectId }, ct);
        }
        catch (EntityNotFoundException) { /* leave Project null — view shows a warning */ }
    }
}
