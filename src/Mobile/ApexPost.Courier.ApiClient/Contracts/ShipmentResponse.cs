namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>A shipment as returned by the v2 API.</summary>
/// <param name="TrackingNumber">Tracking number, <c>APX-</c> plus eight digits.</param>
/// <param name="Recipient">Who receives the shipment.</param>
/// <param name="WeightKg">Weight in kilograms.</param>
/// <param name="Status">Current status.</param>
/// <param name="LatestScan">The newest scan, or <see langword="null"/> before the first scan.</param>
/// <param name="CreatedAtUtc">When the shipment was created (UTC).</param>
/// <param name="Version">Concurrency version of the stored shipment.</param>
public sealed record ShipmentResponse(
    string TrackingNumber,
    RecipientResponse Recipient,
    decimal WeightKg,
    ShipmentStatus Status,
    LatestScanResponse? LatestScan,
    DateTime CreatedAtUtc,
    int Version);
