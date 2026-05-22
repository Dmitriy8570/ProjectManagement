using BusinessLogic.Common;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Commands;
using BusinessLogic.Tasks.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ProjectManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [ProducesResponseType(typeof(CreateProjectTaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateProjectTaskResponse>> CreateTask(
        [FromBody] CreateProjectTaskRequest request, CancellationToken ct)
    {
        var command = new CreateProjectTaskCommand { Data = request };
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetTaskById), new { id = result.Id }, result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProjectTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectTaskDto>> GetTaskById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProjectTaskByIdQuery { Id = id }, ct);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProjectTaskDto>>> GetTasks(
        [FromQuery] ProjectTaskListFilter filter, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProjectTasksQuery { Filter = filter }, ct);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EditProjectTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EditProjectTaskResponse>> EditTask(
        int id, [FromBody] EditProjectTaskRequest request, CancellationToken ct)
    {
        var command = new EditProjectTaskCommand { Data = request, Id = id };
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(int id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteProjectTaskCommand { Id = id }, ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeStatus(
        int id, [FromBody] ChangeProjectTaskStatusRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ChangeProjectTaskStatusCommand { Id = id, Status = request.Status }, ct);
        return NoContent();
    }
}

// Lightweight DTO so the PATCH body is just { "status": "Done" } instead of
// reusing the heavier edit request — keeps the API surface honest about what
// each endpoint actually does.
public record ChangeProjectTaskStatusRequest
{
    public ProjectTaskStatus Status { get; init; }
}
