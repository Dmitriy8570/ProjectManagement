using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Projects.Commands;

public record DeleteProjectCommand : IRequest
{
    public int Id { get; init; }
}

public sealed class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand>
{
    private readonly IProjectRepository _projectRepository;

    public DeleteProjectCommandHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task Handle(DeleteProjectCommand request, CancellationToken ct)
    {
        var deleted = await _projectRepository.DeleteProjectAsync(request.Id, ct);

        if (!deleted)
            throw new EntityNotFoundException(nameof(Project), request.Id);
    }
}
