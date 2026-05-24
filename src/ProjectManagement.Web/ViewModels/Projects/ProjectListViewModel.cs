using BusinessLogic.Projects;

namespace ProjectManagement.Web.ViewModels.Projects;

public sealed class ProjectListViewModel
{
    public string? NameSearch { get; set; }
    public DateTime? StartDateFrom { get; set; }
    public DateTime? StartDateTo { get; set; }
    public int? MinPriority { get; set; }
    public int? MaxPriority { get; set; }
    public ProjectSortBy SortBy { get; set; } = ProjectSortBy.StartDate;
    public bool Descending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public IReadOnlyList<ProjectDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(NameSearch) ||
        StartDateFrom.HasValue || StartDateTo.HasValue ||
        MinPriority.HasValue   || MaxPriority.HasValue;
}
