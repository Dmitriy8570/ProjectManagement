using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Tasks.Queries;

public record GetProjectTasksQuery : IRequest<PagedResult<ProjectTaskDto>>
{
    public ProjectTaskListFilter Filter { get; init; } = new();
}

public class GetProjectTasksQueryHandler
    : IRequestHandler<GetProjectTasksQuery, PagedResult<ProjectTaskDto>>
{
    private const int MaxPageSize = 100;

    private readonly IProjectTaskRepository _taskRepository;

    public GetProjectTasksQueryHandler(IProjectTaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<PagedResult<ProjectTaskDto>> Handle(
        GetProjectTasksQuery request, CancellationToken ct)
    {
        // Clamp page/size in the handler so a malformed query string can't ask
        // for page 0 or pull 100k rows.
        var filter = request.Filter with
        {
            Page = Math.Max(request.Filter.Page, 1),
            PageSize = Math.Clamp(request.Filter.PageSize, 1, MaxPageSize)
        };

        var page = await _taskRepository.GetTasksAsync(filter, ct);
        return new PagedResult<ProjectTaskDto>
        {
            Items = page.Items.Select(t => t.ToDto()).ToArray(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }
}
