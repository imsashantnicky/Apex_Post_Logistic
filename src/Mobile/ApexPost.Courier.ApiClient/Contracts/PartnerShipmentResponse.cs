namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>The partner view of a shipment (no recipient details).</summary>
/// <param name="TrackingNumber">Tracking number.</param>
/// <param name="Status">Current status.</param>
/// <param name="DestinationCity">Destination city.</param>
/// <param name="LatestScan">The newest scan, or <see langword="null"/>.</param>
public sealed record PartnerShipmentResponse(
    string TrackingNumber,
    ShipmentStatus Status,
    string DestinationCity,
    LatestScanResponse? LatestScan);
