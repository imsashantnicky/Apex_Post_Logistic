using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>A stored scan, as returned by <c>GET /api/v2/shipments/{trackingNumber}/scans</c>.</summary>
/// <param name="ScanId">Scan id, <c>scan::{Guid}</c>.</param>
/// <param name="TrackingNumber">Tracking number of the parcel.</param>
/// <param name="ScanType">Kind of scan.</param>
/// <param name="CourierId">Courier who scanned it.</param>
/// <param name="Location">Where the scan happened.</param>
/// <param name="ScannedAtUtc">When the parcel was scanned (UTC).</param>
/// <param name="Notes">Optional courier notes.</param>
/// <param name="ReceivedAtUtc">When the server stored the scan (UTC).</param>
public sealed record ScanResponse(
    string ScanId,
    string TrackingNumber,
    ScanType ScanType,
    string CourierId,
    string Location,
    DateTime ScannedAtUtc,
    string? Notes,
    DateTime ReceivedAtUtc);
