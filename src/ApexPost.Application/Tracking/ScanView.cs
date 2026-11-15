using ApexPost.Domain.Tracking;

namespace ApexPost.Application.Tracking;

/// <summary>A stored scan as callers see it.</summary>
public sealed record ScanView(
    string ScanId,
    string TrackingNumber,
    ScanType ScanType,
    string CourierId,
    string Location,
    DateTime ScannedAtUtc,
    string? Notes,
    DateTime ReceivedAtUtc);
