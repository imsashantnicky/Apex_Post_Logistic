namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>One page of the v2 shipment list.</summary>
/// <param name="Items">Shipments on this page.</param>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="PageSize">Requested page size (1–50).</param>
/// <param name="TotalCount">Shipments matching the filter.</param>
/// <param name="TotalPages">Number of pages.</param>
/// <param name="HasNext">Whether a later page exists.</param>
public sealed record ShipmentPage(
    IReadOnlyList<ShipmentResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNext);
