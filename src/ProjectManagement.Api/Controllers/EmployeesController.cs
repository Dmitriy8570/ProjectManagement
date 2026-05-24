using BusinessLogic.Employees;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Employees.Queries;
using BusinessLogic.Projects.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ProjectManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class EmployeesController : ControllerBase
{
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmployee(int id, CancellationToken ct)
    {
        var command = new DeleteEmployeeCommand { Id = id };
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPut("{id:int}")]
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

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> ListEmployees(
        [FromQuery] string? term,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        var query = new SearchEmployeesQuery { Term = term, Limit = limit };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}

