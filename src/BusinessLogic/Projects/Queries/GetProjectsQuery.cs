using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Projects.Queries;

public record GetProjectsQuery : IRequest<PagedResult<ProjectDto>>
{
    public ProjectListFilter Filter { get; init; } = new();
}

public sealed class GetProjectsQueryHandler
    : IRequestHandler<GetProjectsQuery, PagedResult<ProjectDto>>
{
    private const int MaxPageSize = 100;

    private readonly IProjectRepository _projectRepository;

    public GetProjectsQueryHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<PagedResult<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken ct)
    {
        // Clamp page/size in the handler (mirrors SearchEmployeesQuery) so a
        // malformed query string can't ask for page 0 or pull 100k rows.
        var filter = request.Filter with
        {
            Page = Math.Max(request.Filter.Page, 1),
            PageSize = Math.Clamp(request.Filter.PageSize, 1, MaxPageSize)
        };

        var page = await _projectRepository.GetProjectsAsync(filter, ct);
        return new PagedResult<ProjectDto>
        {
            Items = page.Items.Select(p => p.ToDto()).ToArray(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }
}
