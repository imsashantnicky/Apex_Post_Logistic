namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>Body of <c>PATCH /api/v2/shipments/{trackingNumber}/status</c> (Courier role).</summary>
/// <param name="Status">The new status; only forward moves are accepted.</param>
public sealed record UpdateStatusRequest(ShipmentStatus Status);
