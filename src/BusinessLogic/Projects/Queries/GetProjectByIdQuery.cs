using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Projects.Queries;

public record GetProjectByIdQuery : IRequest<ProjectDto>
{
    public int Id { get; init; }
}

public sealed class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, ProjectDto>
{
    private readonly IProjectRepository _projectRepository;

    public GetProjectByIdQueryHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<ProjectDto> Handle(GetProjectByIdQuery request, CancellationToken ct)
    {
        var project = await _projectRepository.GetProjectByIdAsync(request.Id, ct)
            ?? throw new EntityNotFoundException(nameof(Project), request.Id);

        return project.ToDto();
    }
}
