using BusinessLogic.Common;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using BusinessLogic.Projects.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ProjectManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [ProducesResponseType(typeof(CreateProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateProjectResponse>> CreateProject([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var command = new CreateProjectCommand { Data = request };
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetProjectById), new { id = result.Id }, result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> GetProjectById(int id, CancellationToken ct)
    {
        var query = new GetProjectByIdQuery { Id = id };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProjectDto>>> GetProjects([FromQuery] ProjectListFilter filter, CancellationToken ct)
    {
        var query = new GetProjectsQuery { Filter = filter };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EditProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EditProjectResponse>> EditProject(int id, [FromBody] EditProjectRequest request, CancellationToken ct)
    {
        var command = new EditProjectCommand { Data = request, Id = id };
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(int id, CancellationToken ct)
    {
        var command = new DeleteProjectCommand { Id = id };
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPatch("assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignEmployeeToProject([FromBody] AssignEmployeeToProjectRequest request, CancellationToken ct)
    {
        var command = new AssignEmployeeToProjectCommand { Data = request };
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPatch("unassign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnassignEmployeeFromProject([FromBody] UnassignEmployeeFromProjectRequest request, CancellationToken ct)
    {
        var command = new UnassignEmployeeFromProjectCommand { Data = request };
        await _mediator.Send(command, ct);
        return NoContent();
    }
}
