using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Tasks.Commands;

public record DeleteProjectTaskCommand : IRequest
{
    public int Id { get; init; }
}

public sealed class DeleteProjectTaskCommandHandler : IRequestHandler<DeleteProjectTaskCommand>
{
    private readonly IProjectTaskRepository _taskRepository;

    public DeleteProjectTaskCommandHandler(IProjectTaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task Handle(DeleteProjectTaskCommand request, CancellationToken ct)
    {
        var deleted = await _taskRepository.DeleteTaskAsync(request.Id, ct);

        if (!deleted)
            throw new EntityNotFoundException(nameof(ProjectTask), request.Id);
    }
}
