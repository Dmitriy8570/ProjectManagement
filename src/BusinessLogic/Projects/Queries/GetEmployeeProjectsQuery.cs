using MediatR;

namespace BusinessLogic.Projects.Queries;

public record GetEmployeeProjectsQuery : IRequest<EmployeeProjectsDto>
{
    public int EmployeeId { get; init; }
}

public record EmployeeProjectsDto
{
    public IReadOnlyList<ProjectDto> ManagedProjects { get; init; } = [];
    public IReadOnlyList<ProjectDto> ParticipantProjects { get; init; } = [];
}

public class GetEmployeeProjectsQueryHandler
    : IRequestHandler<GetEmployeeProjectsQuery, EmployeeProjectsDto>
{
    private readonly IProjectRepository _repository;

    public GetEmployeeProjectsQueryHandler(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<EmployeeProjectsDto> Handle(GetEmployeeProjectsQuery request, CancellationToken ct)
    {
        var managedTask = _repository.GetProjectsAsync(
            new ProjectListFilter { ProjectManagerId = request.EmployeeId, PageSize = 100 }, ct);

        var participantTask = _repository.GetProjectsAsync(
            new ProjectListFilter { ParticipantEmployeeId = request.EmployeeId, PageSize = 100 }, ct);

        await Task.WhenAll(managedTask, participantTask);

        return new EmployeeProjectsDto
        {
            ManagedProjects    = managedTask.Result.Items.Select(p => p.ToDto()).ToArray(),
            ParticipantProjects = participantTask.Result.Items.Select(p => p.ToDto()).ToArray()
        };
    }
}
