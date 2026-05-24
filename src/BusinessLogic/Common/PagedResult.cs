namespace BusinessLogic.Common;

/// <summary>
/// Carries one page of items together with the total count of matches across
/// all pages. <see cref="TotalCount"/> is what the UI needs to render a pager
/// (page numbers, "X of Y" labels) without a second round-trip.
/// </summary>
public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
