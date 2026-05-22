using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Tasks.Queries;

public record GetProjectTaskByIdQuery : IRequest<ProjectTaskDto>
{
    public int Id { get; init; }
}

public class GetProjectTaskByIdQueryHandler : IRequestHandler<GetProjectTaskByIdQuery, ProjectTaskDto>
{
    private readonly IProjectTaskRepository _taskRepository;

    public GetProjectTaskByIdQueryHandler(IProjectTaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<ProjectTaskDto> Handle(GetProjectTaskByIdQuery request, CancellationToken ct)
    {
        var task = await _taskRepository.GetTaskByIdAsync(request.Id, ct)
            ?? throw new EntityNotFoundException(nameof(ProjectTask), request.Id);

        return task.ToDto();
    }
}
