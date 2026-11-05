namespace ApexPost.Api.Contracts.V1;

/// <summary>One page of a list, with what a client needs to ask for the next one.</summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNext);
