using ApexPost.Domain.Tracking;

namespace ApexPost.Application.Tracking;

/// <summary>One scan in an upload from a courier device, not yet checked.</summary>
public sealed record ScanUpload(
    string ScanId,
    string TrackingNumber,
    ScanType ScanType,
    string CourierId,
    string Location,
    DateTime ScannedAtUtc,
    string? Notes)
{
    /// <summary>The scan fields the domain checks and stores.</summary>
    public ScanDetails Details => new(ScanId, ScanType, CourierId, Location, ScannedAtUtc, Notes);
}
