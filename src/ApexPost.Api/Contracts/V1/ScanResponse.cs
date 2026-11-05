using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>A stored scan of a shipment.</summary>
/// <param name="ScanId">The device document id.</param>
/// <param name="TrackingNumber">The scanned shipment.</param>
/// <param name="ScanType">What the courier recorded.</param>
/// <param name="CourierId">The courier.</param>
/// <param name="Location">Where the scan happened.</param>
/// <param name="ScannedAtUtc">When the scan happened.</param>
/// <param name="Notes">Free text from the courier.</param>
/// <param name="ReceivedAtUtc">When the server stored the scan.</param>
public sealed record ScanResponse(
    string ScanId,
    string TrackingNumber,
    ScanType ScanType,
    string CourierId,
    string Location,
    DateTime ScannedAtUtc,
    string? Notes,
    DateTime ReceivedAtUtc)
{
    /// <summary>The device document id, scan:: followed by a GUID.</summary>
    /// <example>scan::0f8fad5b-d9cb-469f-a165-70867728950e</example>
    public string ScanId { get; init; } = ScanId;

    /// <summary>The scanned shipment.</summary>
    /// <example>APX-20000003</example>
    public string TrackingNumber { get; init; } = TrackingNumber;

    /// <summary>What the courier recorded as a number: 1 Pickup, 2 InTransit, 3 Delivered, 4 Failed.</summary>
    /// <example>1</example>
    public ScanType ScanType { get; init; } = ScanType;

    /// <summary>The courier, CR- followed by 3 digits.</summary>
    /// <example>CR-101</example>
    public string CourierId { get; init; } = CourierId;

    /// <summary>Where the scan happened.</summary>
    /// <example>Westfield depot</example>
    public string Location { get; init; } = Location;

    /// <summary>When the scan happened, in UTC.</summary>
    /// <example>2026-11-05T09:30:00.0000000Z</example>
    public DateTime ScannedAtUtc { get; init; } = ScannedAtUtc;

    /// <summary>Free text from the courier, if any.</summary>
    /// <example>Collected from reception.</example>
    public string? Notes { get; init; } = Notes;

    /// <summary>When the server stored the scan, in UTC.</summary>
    /// <example>2026-11-05T09:31:12.0000000Z</example>
    public DateTime ReceivedAtUtc { get; init; } = ReceivedAtUtc;
}
