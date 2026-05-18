using BusinessLogic.Common;
using BusinessLogic.Employees;
using MediatR;

namespace BusinessLogic.Projects.Commands;

public record AssignEmployeeToProjectRequest
{
    public int ProjectId { get; init; }
    public int EmployeeId { get; init; }
}

public record AssignEmployeeToProjectCommand : IRequest
{
    public AssignEmployeeToProjectRequest Data { get; init; } = default!;
}

public class AssignEmployeeToProjectCommandHandler : IRequestHandler<AssignEmployeeToProjectCommand>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public AssignEmployeeToProjectCommandHandler(
        IProjectRepository projectRepository,
        IEmployeeRepository employeeRepository)
    {
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task Handle(AssignEmployeeToProjectCommand request, CancellationToken ct)
    {
        var project = await _projectRepository.GetProjectByIdAsync(request.Data.ProjectId, ct)
            ?? throw new EntityNotFoundException(nameof(Project), request.Data.ProjectId);

        var employee = await _employeeRepository.GetEmployeeByIdAsync(request.Data.EmployeeId, ct)
            ?? throw new EntityNotFoundException(nameof(Employee), request.Data.EmployeeId);

        // Project.AddEmployee is idempotent — re-assigning an existing member
        // is a no-op rather than an error, which is the friendly behavior for
        // checkbox-style multi-select UIs.
        project.AddEmployee(employee);
        await _projectRepository.SaveAsync(ct);
    }
}
