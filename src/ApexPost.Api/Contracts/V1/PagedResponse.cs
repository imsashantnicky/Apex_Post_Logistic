namespace ApexPost.Api.Contracts.V1;

/// <summary>One page of a list, with what a client needs to ask for the next one.</summary>
/// <typeparam name="T">The type of the items.</typeparam>
/// <param name="Items">The items of this page.</param>
/// <param name="Page">The page number.</param>
/// <param name="PageSize">The requested page size.</param>
/// <param name="TotalCount">All matching items.</param>
/// <param name="TotalPages">All pages.</param>
/// <param name="HasNext">Whether a next page exists.</param>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNext)
{
    /// <summary>The items of this page, at most <see cref="PageSize"/>.</summary>
    public IReadOnlyList<T> Items { get; init; } = Items;

    /// <summary>The page number, starting at 1.</summary>
    /// <example>1</example>
    public int Page { get; init; } = Page;

    /// <summary>The requested page size, 1 to 50.</summary>
    /// <example>20</example>
    public int PageSize { get; init; } = PageSize;

    /// <summary>How many items match the filter on all pages.</summary>
    /// <example>10</example>
    public int TotalCount { get; init; } = TotalCount;

    /// <summary>How many pages there are.</summary>
    /// <example>1</example>
    public int TotalPages { get; init; } = TotalPages;

    /// <summary>Whether a next page exists.</summary>
    /// <example>false</example>
    public bool HasNext { get; init; } = HasNext;
}
