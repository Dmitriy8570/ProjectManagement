namespace BusinessLogic.Projects;

public enum ProjectSortBy
{
    Name,
    StartDate,
    EndDate,
    Priority
}

/// <summary>
/// Filter + sort options for listing projects. Lives in BusinessLogic because
/// it is part of the repository contract, not a presentation-layer concern —
/// the data layer translates this into its own query language.
/// </summary>
public record ProjectListFilter
{
    public DateTime? StartDateFrom { get; init; }
    public DateTime? StartDateTo { get; init; }
    public int? MinPriority { get; init; }
    public int? MaxPriority { get; init; }
    public int? ProjectManagerId { get; init; }

    public ProjectSortBy SortBy { get; init; } = ProjectSortBy.StartDate;
    public bool Descending { get; init; }
}
