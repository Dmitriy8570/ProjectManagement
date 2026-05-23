using BusinessLogic.Common;
using BusinessLogic.Identity;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using BusinessLogic.Projects.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProjectManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
// Role rules (per spec, identical to the Web layer):
//   Руководитель      — full access.
//   Менеджер проекта  — sees and edits ONLY projects where they are the PM.
//   Сотрудник         — sees ONLY projects they participate in, read-only.
// Index filters at the query level; write-side endpoints add a resource
// check after loading the project so URL guessing also returns 403.
public class ProjectsController : ControllerBase
{
    private const string DirectorOrPm = Roles.Director + "," + Roles.ProjectManager;

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ProjectsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Roles = Roles.Director)]
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectDto>> GetProjectById(int id, CancellationToken ct)
    {
        var project = await _mediator.Send(new GetProjectByIdQuery { Id = id }, ct);
        if (!CanView(project))
            return Forbid();
        return Ok(project);
    }

    /// <summary>
    /// Paginated/filtered project listing. For non-Director callers the
    /// filter is narrowed at the query level (ProjectManagerId or
    /// ParticipantEmployeeId), so the SPA never even sees forbidden rows.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProjectDto>>> GetProjects([FromQuery] ProjectListFilter filter, CancellationToken ct)
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

        var query = new GetProjectsQuery { Filter = filter };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = DirectorOrPm)]
    [ProducesResponseType(typeof(EditProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EditProjectResponse>> EditProject(int id, [FromBody] EditProjectRequest request, CancellationToken ct)
    {
        if (!await CanManageAsync(id, ct))
            return Forbid();

        var command = new EditProjectCommand { Data = request, Id = id };
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Director)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(int id, CancellationToken ct)
    {
        var command = new DeleteProjectCommand { Id = id };
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPatch("assign")]
    [Authorize(Roles = DirectorOrPm)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignEmployeeToProject([FromBody] AssignEmployeeToProjectRequest request, CancellationToken ct)
    {
        if (!await CanManageAsync(request.ProjectId, ct))
            return Forbid();

        var command = new AssignEmployeeToProjectCommand { Data = request };
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPatch("unassign")]
    [Authorize(Roles = DirectorOrPm)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UnassignEmployeeFromProject([FromBody] UnassignEmployeeFromProjectRequest request, CancellationToken ct)
    {
        if (!await CanManageAsync(request.ProjectId, ct))
            return Forbid();

        var command = new UnassignEmployeeFromProjectCommand { Data = request };
        await _mediator.Send(command, ct);
        return NoContent();
    }

    // ── Authorization helpers ────────────────────────────────────────────
    // Same shape as the Web counterpart: Director bypasses, others must be
    // either PM (CanManage) or PM/participant (CanView) of the project.

    private bool CanView(ProjectDto project)
    {
        if (User.IsInRole(Roles.Director))
            return true;

        var empId = _currentUser.EmployeeId;
        if (empId is null)
            return false;

        return project.ProjectManager.Id == empId
            || project.Employees.Any(e => e.Id == empId);
    }

    private bool CanManage(ProjectDto project)
    {
        if (User.IsInRole(Roles.Director))
            return true;

        var empId = _currentUser.EmployeeId;
        return empId is not null && project.ProjectManager.Id == empId;
    }

    private async Task<bool> CanManageAsync(int projectId, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Director))
            return true;
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = projectId }, ct);
            return CanManage(project);
        }
        catch (EntityNotFoundException)
        {
            return false;
        }
    }
}
