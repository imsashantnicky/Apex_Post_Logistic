using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>A stored scan of a shipment.</summary>
public sealed record ScanResponse(
    string ScanId,
    string TrackingNumber,
    ScanType ScanType,
    string CourierId,
    string Location,
    DateTime ScannedAtUtc,
    string? Notes,
    DateTime ReceivedAtUtc);
