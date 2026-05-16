using System.ComponentModel.DataAnnotations;
using BusinessLogic.Common;
using BusinessLogic.Employees;
using MediatR;

namespace BusinessLogic.Projects.Commands;

public record CreateProjectRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = default!;

    [Required, MaxLength(200)]
    public string CustomerCompany { get; init; } = default!;

    [Required, MaxLength(200)]
    public string ExecutingCompany { get; init; } = default!;

    [Required]
    public DateTime StartDate { get; init; }

    [Required]
    public DateTime EndDate { get; init; }

    [Required]
    public int ProjectManagerId { get; init; }

    // Members are optional: a freshly-created project may start with just a
    // project manager and have participants assigned later.
    public List<int> EmployeeIds { get; init; } = new();

    [Required, Range(0, int.MaxValue)]
    public int Priority { get; init; }
}

public record CreateProjectCommand : IRequest<CreateProjectResponse>
{
    public CreateProjectRequest Data { get; init; } = default!;
}

public record CreateProjectResponse
{
    public int Id { get; init; }
}

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, CreateProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public CreateProjectCommandHandler(
        IProjectRepository projectRepository,
        IEmployeeRepository employeeRepository)
    {
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<CreateProjectResponse> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        var data = request.Data;

        // The project manager must already exist; we load the entity so the
        // domain constructor can take an Employee rather than a raw id —
        // that way you can't build a Project pointing at a phantom manager.
        var projectManager = await _employeeRepository.GetEmployeeByIdAsync(data.ProjectManagerId, ct)
            ?? throw new EntityNotFoundException(nameof(Employee), data.ProjectManagerId);

        // Resolve all participants up-front so a bad id fails fast, before
        // we touch the database with a half-built project.
        var participants = new List<Employee>(capacity: data.EmployeeIds.Count);
        foreach (var employeeId in data.EmployeeIds.Distinct())
        {
            var employee = await _employeeRepository.GetEmployeeByIdAsync(employeeId, ct)
                ?? throw new EntityNotFoundException(nameof(Employee), employeeId);

            participants.Add(employee);
        }

        var project = new Project(
            data.Name,
            data.CustomerCompany,
            data.ExecutingCompany,
            data.StartDate,
            data.EndDate,
            projectManager,
            data.Priority);

        foreach (var employee in participants)
            project.AddEmployee(employee);

        await _projectRepository.AddProjectAsync(project, ct);
        await _projectRepository.SaveAsync(ct);

        return new CreateProjectResponse { Id = project.Id };
    }
}
