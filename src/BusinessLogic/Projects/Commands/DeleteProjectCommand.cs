using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Projects.Commands;

public record DeleteProjectCommand : IRequest
{
    public int Id { get; init; }
}

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand>
{
    private readonly IProjectRepository _projectRepository;

    public DeleteProjectCommandHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task Handle(DeleteProjectCommand request, CancellationToken ct)
    {
        if (await _projectRepository.GetProjectByIdAsync(request.Id, ct) is null)
            throw new EntityNotFoundException(nameof(Project), request.Id);

        await _projectRepository.DeleteProjectAsync(request.Id, ct);
        await _projectRepository.SaveAsync(ct);
    }
}
