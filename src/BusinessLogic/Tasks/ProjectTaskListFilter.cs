namespace BusinessLogic.Tasks;

public enum ProjectTaskSortBy
{
    Name,
    Priority,
    Status
}

/// <summary>
/// Filter + sort + paging options for listing tasks. Lives in BusinessLogic
/// because it is part of the repository contract.
/// </summary>
public record ProjectTaskListFilter
{
    public int? ProjectId { get; init; }
    public int? AssigneeId { get; init; }
    public int? AuthorId { get; init; }
    public ProjectTaskStatus? Status { get; init; }
    public int? MinPriority { get; init; }
    public int? MaxPriority { get; init; }
    public string? NameSearch { get; init; }

    public ProjectTaskSortBy SortBy { get; init; } = ProjectTaskSortBy.Priority;
    public bool Descending { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
