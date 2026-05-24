using BusinessLogic.Employees;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Employees.Queries;
using BusinessLogic.Identity;
using BusinessLogic.Projects.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProjectManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
// Mirrors the Web layer: only Руководитель manages the directory. Detail and
// per-employee project lists stay open to any authenticated user so the SPA
// can render names that appear inside ProjectDtos (PM / participants); Search
// is open to PM as well — the project wizard needs it.
public sealed class EmployeesController : ControllerBase
{
    private const string DirectorOrPm = Roles.Director + "," + Roles.ProjectManager;

    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetEmployeeById(int id, CancellationToken ct)
    {
        var query = new GetEmployeeByIdQuery { Id = id };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Director)]
    [ProducesResponseType(typeof(CreateEmployeeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateEmployeeResponse>> CreateEmployee(
        [FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var command = new CreateEmployeeCommand { Data = request };
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetEmployeeById), new { id = result.Id }, result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Director)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmployee(int id, CancellationToken ct)
    {
        var command = new DeleteEmployeeCommand { Id = id };
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Director)]
    [ProducesResponseType(typeof(EditEmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EditEmployeeResponse>> UpdateEmployee(int id, [FromBody] EditEmployeeRequest request, CancellationToken ct)
    {
        var command = new EditEmployeeCommand { Data = request, Id = id };
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}/projects")]
    [ProducesResponseType(typeof(EmployeeProjectsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeProjectsDto>> GetEmployeeProjects(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetEmployeeProjectsQuery { EmployeeId = id }, ct);
        return Ok(result);
    }

    /// <summary>
    /// Lists employees, optionally restricted by a free-text term and/or role
    /// whitelist (comma-separated). The PM picker on the SPA passes
    /// <c>roles=Director,ProjectManager</c> to hide plain Сотрудник users —
    /// CreateProjectCommandHandler re-validates the rule on submit.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = DirectorOrPm)]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> ListEmployees(
        [FromQuery] string? term,
        [FromQuery] int limit,
        [FromQuery] string? roles,
        CancellationToken ct)
    {
        var query = new SearchEmployeesQuery
        {
            Term = term,
            Limit = limit,
            Roles = ParseRoles(roles)
        };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
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
