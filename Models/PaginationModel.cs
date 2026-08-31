namespace AFTRS.Models;

/// <summary>
/// Generic pagination model for server-side pagination support.
/// Tracks pagination state including current page, page size, and total item count.
/// </summary>
public class PaginationModel<T>
{
    /// <summary>
    /// The paginated items for the current page.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = new List<T>();

    /// <summary>
    /// The current page number (1-indexed).
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Total number of items across all pages (before pagination).
    /// </summary>
    public int TotalItems { get; set; } = 0;

    /// <summary>
    /// Calculated total number of pages.
    /// </summary>
    public int TotalPages => TotalItems == 0 ? 1 : (int)Math.Ceiling((decimal)TotalItems / PageSize);

    /// <summary>
    /// Whether there are items on the next page.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Whether there are items on the previous page.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// The 1-indexed row number of the first item on the current page.
    /// </summary>
    public int StartRowNumber => (CurrentPage - 1) * PageSize + 1;

    /// <summary>
    /// The 1-indexed row number of the last item on the current page.
    /// </summary>
    public int EndRowNumber => Math.Min(CurrentPage * PageSize, TotalItems);
}

/// <summary>
/// Non-generic version for simpler use cases.
/// </summary>
public class PaginationMetadata
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; } = 0;
    public int TotalPages => TotalItems == 0 ? 1 : (int)Math.Ceiling((decimal)TotalItems / PageSize);
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;
    public int StartRowNumber => (CurrentPage - 1) * PageSize + 1;
    public int EndRowNumber => Math.Min(CurrentPage * PageSize, TotalItems);
}
