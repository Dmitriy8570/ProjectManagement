using BusinessLogic.Projects;
using BusinessLogic.Tasks;

namespace ProjectManagement.Web.ViewModels.Tasks;

public sealed class TaskListViewModel
{
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    // Full project record loaded when scoped — populated server-side so the
    // context banner can show dates and manager without an extra API call.
    public ProjectDto? Project { get; set; }

    public string? NameSearch { get; set; }
    public ProjectTaskStatus? Status { get; set; }
    public int? MinPriority { get; set; }
    public int? MaxPriority { get; set; }
    public ProjectTaskSortBy SortBy { get; set; } = ProjectTaskSortBy.Priority;
    public bool Descending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public IReadOnlyList<ProjectTaskDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(NameSearch) ||
        Status.HasValue ||
        MinPriority.HasValue || MaxPriority.HasValue;
}
