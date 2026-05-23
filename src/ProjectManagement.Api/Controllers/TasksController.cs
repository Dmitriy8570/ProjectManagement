using BusinessLogic.Common;
using BusinessLogic.Identity;
using BusinessLogic.Projects.Queries;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Commands;
using BusinessLogic.Tasks.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProjectManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class TasksController : ControllerBase
{
    private const string DirectorOrPm = Roles.Director + "," + Roles.ProjectManager;

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public TasksController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Roles = DirectorOrPm)]
    [ProducesResponseType(typeof(CreateProjectTaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreateProjectTaskResponse>> CreateTask(
        [FromBody] CreateProjectTaskRequest request, CancellationToken ct)
    {
        if (!await CanManageProjectAsync(request.ProjectId, ct))
            return Forbid();

        var command = new CreateProjectTaskCommand { Data = request };
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetTaskById), new { id = result.Id }, result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProjectTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectTaskDto>> GetTaskById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);
        if (!await CanViewProjectAsync(result.ProjectId, ct))
            return Forbid();
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProjectTaskDto>>> GetTasks(
        [FromQuery] ProjectTaskListFilter filter, CancellationToken ct)
    {
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
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = DirectorOrPm)]
    [ProducesResponseType(typeof(EditProjectTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EditProjectTaskResponse>> EditTask(
        int id, [FromBody] EditProjectTaskRequest request, CancellationToken ct)
    {
        var task = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);
        if (!await CanManageProjectAsync(task.ProjectId, ct))
            return Forbid();

        var command = new EditProjectTaskCommand { Data = request, Id = id };
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = DirectorOrPm)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteTask(int id, CancellationToken ct)
    {
        var task = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);
        if (!await CanManageProjectAsync(task.ProjectId, ct))
            return Forbid();

        await _mediator.Send(new DeleteProjectTaskCommand { Id = id }, ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangeStatus(
        int id, [FromBody] ChangeProjectTaskStatusRequest request, CancellationToken ct)
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

        await _mediator.Send(new ChangeProjectTaskStatusCommand { Id = id, Status = request.Status }, ct);
        return NoContent();
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
}

public record ChangeProjectTaskStatusRequest
{
    public ProjectTaskStatus Status { get; init; }
}
