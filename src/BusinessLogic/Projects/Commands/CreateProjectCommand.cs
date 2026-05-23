using System.ComponentModel.DataAnnotations;
using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Identity;
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

    public DateTime StartDate { get; init; }

    public DateTime EndDate { get; init; }

    [Range(0, int.MaxValue)]
    public int ProjectManagerId { get; init; }

    // Members are optional: a freshly-created project may start with just a
    // project manager and have participants assigned later.
    public List<int> EmployeeIds { get; init; } = [];

    [Range(0, int.MaxValue)]
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

public sealed class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, CreateProjectResponse>
{
    /// <summary>
    /// Only users carrying one of these roles may be appointed as a project
    /// manager — a plain Employee is not eligible.
    /// </summary>
    private static readonly string[] EligiblePmRoles = [Roles.Director, Roles.ProjectManager];

    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUserAccountService _userAccountService;

    public CreateProjectCommandHandler(
        IProjectRepository projectRepository,
        IEmployeeRepository employeeRepository,
        IUserAccountService userAccountService)
    {
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
        _userAccountService = userAccountService;
    }

    public async Task<CreateProjectResponse> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        var data = request.Data;

        // The project manager must already exist; we load the entity so the
        // domain constructor can take an Employee rather than a raw id —
        // that way you can't build a Project pointing at a phantom manager.
        var projectManager = await _employeeRepository.GetEmployeeByIdAsync(data.ProjectManagerId, ct)
            ?? throw new EntityNotFoundException(nameof(Employee), data.ProjectManagerId);

        // Block a plain Employee from being promoted to PM through the form.
        // The UI's autocomplete already filters by role, but a hand-crafted
        // POST would otherwise sneak around it.
        if (!await _userAccountService.IsEmployeeInAnyRoleAsync(projectManager.Id, EligiblePmRoles, ct))
            throw new DomainValidationException(
                "Selected employee cannot be a project manager — they must have the " +
                "Director or ProjectManager role.");

        // PM and participants are disjoint sets by domain invariant; quietly
        // drop the PM from the participant list rather than failing the
        // request — the wizard's two steps are filled independently.
        var participantIds = data.EmployeeIds
            .Where(id => id != data.ProjectManagerId)
            .Distinct()
            .ToArray();

        // Resolve all participants in one round-trip so a bad id fails fast,
        // before we touch the database with a half-built project.
        var participants = await _employeeRepository.GetEmployeesByIdsAsync(participantIds, ct);
        if (participants.Count != participantIds.Length)
        {
            var foundIds = participants.Select(e => e.Id).ToHashSet();
            var missingId = participantIds.First(id => !foundIds.Contains(id));
            throw new EntityNotFoundException(nameof(Employee), missingId);
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
