using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Tasks.Commands;

public record ChangeProjectTaskStatusCommand : IRequest
{
    public int Id { get; init; }
    public ProjectTaskStatus Status { get; init; }
}

public class ChangeProjectTaskStatusCommandHandler : IRequestHandler<ChangeProjectTaskStatusCommand>
{
    private readonly IProjectTaskRepository _taskRepository;

    public ChangeProjectTaskStatusCommandHandler(IProjectTaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task Handle(ChangeProjectTaskStatusCommand request, CancellationToken ct)
    {
        var task = await _taskRepository.GetTaskByIdAsync(request.Id, ct)
            ?? throw new EntityNotFoundException(nameof(ProjectTask), request.Id);

        task.ChangeStatus(request.Status);
        await _taskRepository.SaveAsync(ct);
    }
}
