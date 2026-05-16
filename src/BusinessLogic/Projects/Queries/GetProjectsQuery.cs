using MediatR;

namespace BusinessLogic.Projects.Queries;

public record GetProjectsQuery : IRequest<IReadOnlyList<ProjectDto>>
{
    public ProjectListFilter Filter { get; init; } = new();
}

public class GetProjectsQueryHandler
    : IRequestHandler<GetProjectsQuery, IReadOnlyList<ProjectDto>>
{
    private readonly IProjectRepository _projectRepository;

    public GetProjectsQueryHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<IReadOnlyList<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken ct)
    {
        var projects = await _projectRepository.GetProjectsAsync(request.Filter, ct);
        return projects.Select(p => p.ToDto()).ToArray();
    }
}
