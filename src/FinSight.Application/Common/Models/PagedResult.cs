namespace FinSight.Application.Common.Models;

/// <summary>
/// Paginated list wrapper used by query handlers that return collections.
/// Carries the items, total count, and current page info so the API
/// can return proper pagination metadata in response headers.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
        => new() { Items = items, TotalCount = totalCount, PageNumber = pageNumber, PageSize = pageSize };
}
