using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>The newest scan of a shipment, as embedded in v2 shipment responses.</summary>
/// <param name="ScanType">Kind of scan.</param>
/// <param name="Location">Where the scan happened.</param>
/// <param name="ScannedAtUtc">When the courier scanned the parcel (UTC).</param>
public sealed record LatestScanResponse(ScanType ScanType, string Location, DateTime ScannedAtUtc);
