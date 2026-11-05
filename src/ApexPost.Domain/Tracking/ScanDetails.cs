namespace ApexPost.Domain.Tracking;

/// <summary>The fields a courier device sends for one scan, before they are checked.</summary>
public sealed record ScanDetails(
    string ScanId,
    ScanType ScanType,
    string CourierId,
    string Location,
    DateTime ScannedAtUtc,
    string? Notes);
