using BusinessLogic.Common;
using BusinessLogic.Identity;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Queries;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Commands;
using BusinessLogic.Tasks.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Web.ViewModels.Tasks;

namespace ProjectManagement.Web.Controllers;

[Route("tasks")]
[Authorize]
public sealed class TasksController : Controller
{
    private const string DirectorOrPm = Roles.Director + "," + Roles.ProjectManager;

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public TasksController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

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

        if (!User.IsInRole(Roles.Director))
        {
            var empId = _currentUser.EmployeeId;
            if (empId is null)
                return Forbid();

            filter = User.IsInRole(Roles.ProjectManager)
                ? filter with { ProjectManagerId = empId }
                : filter with { ParticipantEmployeeId = empId };
        }

        var result = await _mediator.Send(new GetProjectTasksQuery { Filter = filter }, ct);
        vm.Items = result.Items;
        vm.TotalCount = result.TotalCount;

        if (vm.ProjectId.HasValue)
        {
            try
            {
                var project = await _mediator.Send(new GetProjectByIdQuery { Id = vm.ProjectId.Value }, ct);
                vm.Project = project;
                vm.ProjectName = project.Name;
            }
            catch (EntityNotFoundException) { }
        }

        return View(vm);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        try
        {
            var task = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);
            if (!await CanViewProjectAsync(task.ProjectId, ct))
                return Forbid();
            return View(task);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("create")]
    [Authorize(Roles = DirectorOrPm)]
    public async Task<IActionResult> Create([FromQuery] int? projectId, CancellationToken ct)
    {
        if (projectId.HasValue && !await CanManageProjectAsync(projectId.Value, ct))
            return Forbid();

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
    [Authorize(Roles = DirectorOrPm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTaskViewModel model, CancellationToken ct)
    {
        if (!await CanManageProjectAsync(model.ProjectId, ct))
            return Forbid();

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
    [Authorize(Roles = DirectorOrPm)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        try
        {
            var task = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);
            if (!await CanManageProjectAsync(task.ProjectId, ct))
                return Forbid();

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
    [Authorize(Roles = DirectorOrPm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditTaskViewModel model, CancellationToken ct)
    {
        if (!await CanManageProjectAsync(model.ProjectId, ct))
            return Forbid();

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
    [Authorize(Roles = DirectorOrPm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var task = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);
            if (!await CanManageProjectAsync(task.ProjectId, ct))
                return Forbid();

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
            var task = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);

            if (User.IsInRole(Roles.Director))
            {
                // Director can change any task's status.
            }
            else if (User.IsInRole(Roles.ProjectManager))
            {
                if (!await CanManageProjectAsync(task.ProjectId, ct))
                    return Forbid();
            }
            else
            {
                var empId = _currentUser.EmployeeId;
                if (empId is null || task.Assignee.Id != empId)
                    return Forbid();
            }

            await _mediator.Send(new ChangeProjectTaskStatusCommand { Id = id, Status = status }, ct);
            TempData["Success"] = "Status updated.";
        }
        catch (EntityNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("project-members")]
    public async Task<IActionResult> ProjectMembers([FromQuery] int projectId, CancellationToken ct)
    {
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = projectId }, ct);
            List<object> members = [new { id = project.ProjectManager.Id, fullName = project.ProjectManager.FullName }];
            members.AddRange(project.Employees.Select(e => new { id = e.Id, fullName = e.FullName }));
            return Json(members);
        }
        catch (EntityNotFoundException)
        {
            return Json(Array.Empty<object>());
        }
    }

    // ── Authorization helpers ────────────────────────────────────────────

    private async Task<bool> CanViewProjectAsync(int projectId, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Director))
            return true;
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = projectId }, ct);
            var empId = _currentUser.EmployeeId;
            if (empId is null) return false;
            return project.ProjectManager.Id == empId
                || project.Employees.Any(e => e.Id == empId);
        }
        catch (EntityNotFoundException)
        {
            return false;
        }
    }

    private async Task<bool> CanManageProjectAsync(int projectId, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Director))
            return true;
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = projectId }, ct);
            var empId = _currentUser.EmployeeId;
            return empId is not null && project.ProjectManager.Id == empId;
        }
        catch (EntityNotFoundException)
        {
            return false;
        }
    }

    private async Task PopulateCreateLookups(CreateTaskViewModel vm, int? projectId, CancellationToken ct)
    {
        var projectFilter = new ProjectListFilter { PageSize = 100, SortBy = ProjectSortBy.Name };

        if (!User.IsInRole(Roles.Director))
        {
            var empId = _currentUser.EmployeeId;
            if (empId is not null && User.IsInRole(Roles.ProjectManager))
                projectFilter = projectFilter with { ProjectManagerId = empId };
        }

        var projectsPage = await _mediator.Send(
            new GetProjectsQuery { Filter = projectFilter }, ct);
        vm.AvailableProjects = projectsPage.Items;

        if (projectId.HasValue)
        {
            try
            {
                vm.SelectedProject = await _mediator.Send(new GetProjectByIdQuery { Id = projectId.Value }, ct);
            }
            catch (EntityNotFoundException) { }
        }
    }

    private async Task PopulateEditLookups(EditTaskViewModel vm, CancellationToken ct)
    {
        try
        {
            vm.Project = await _mediator.Send(new GetProjectByIdQuery { Id = vm.ProjectId }, ct);
        }
        catch (EntityNotFoundException) { }
    }
}
