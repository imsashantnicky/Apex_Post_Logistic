using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.Domain.Scans;

/// <summary>
/// One scan of a shipment by a courier. Scans are recorded offline first, so every new scan is
/// <see cref="SyncStatus.Pending"/> until an upload succeeds.
/// </summary>
public sealed class ShipmentScan
{
    private ShipmentScan(
        string id,
        string trackingNumber,
        ScanType scanType,
        string courierId,
        string location,
        DateTimeOffset scannedAtUtc,
        string? notes,
        SyncStatus syncStatus)
    {
        if (!ScanIds.IsValid(id))
        {
            throw new ArgumentException($"Scan id must look like '{ScanIds.Prefix}{{guid}}'.", nameof(id));
        }

        Id = id;
        TrackingNumber = Matching(
            Required(trackingNumber, nameof(trackingNumber)).ToUpperInvariant(),
            ScanRules.IsValidTrackingNumber, ScanRules.TrackingNumberPattern, nameof(trackingNumber));
        ScanType = Defined(scanType, nameof(scanType));
        CourierId = Matching(
            Required(courierId, nameof(courierId)),
            ScanRules.IsValidCourierId, ScanRules.CourierIdPattern, nameof(courierId));
        Location = Required(location, nameof(location));
        ScannedAtUtc = scannedAtUtc.ToUniversalTime();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SyncStatus = Defined(syncStatus, nameof(syncStatus));
    }

    /// <summary>The document id, <c>scan::{Guid}</c>.</summary>
    public string Id { get; }

    /// <summary>The shipment's tracking number, trimmed and upper-case.</summary>
    public string TrackingNumber { get; }

    /// <summary>What happened to the shipment.</summary>
    public ScanType ScanType { get; }

    /// <summary>The courier who made the scan.</summary>
    public string CourierId { get; }

    /// <summary>Where the scan happened, for example a branch code or a street address.</summary>
    public string Location { get; }

    /// <summary>When the scan happened, always in UTC.</summary>
    public DateTimeOffset ScannedAtUtc { get; }

    /// <summary>Optional free text from the courier.</summary>
    public string? Notes { get; }

    /// <summary>Whether the scan has reached the server.</summary>
    public SyncStatus SyncStatus { get; private set; }

    /// <summary>
    /// Records a new scan now. The scan gets a fresh id and starts as <see cref="SyncStatus.Pending"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A required value is missing, the tracking number or courier id does not match <see cref="ScanRules"/>,
    /// or the scan type is unknown.
    /// </exception>
    public static ShipmentScan CreateNew(
        string trackingNumber,
        ScanType scanType,
        string courierId,
        string location,
        string? notes,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return new ShipmentScan(
            ScanIds.New(), trackingNumber, scanType, courierId, location,
            timeProvider.GetUtcNow(), notes, SyncStatus.Pending);
    }

    /// <summary>Rebuilds a scan that was stored earlier, applying the same validation as a new scan.</summary>
    /// <exception cref="ArgumentException">The stored values are not a valid scan.</exception>
    public static ShipmentScan Restore(
        string id,
        string trackingNumber,
        ScanType scanType,
        string courierId,
        string location,
        DateTimeOffset scannedAtUtc,
        string? notes,
        SyncStatus syncStatus) =>
        new(id, trackingNumber, scanType, courierId, location, scannedAtUtc, notes, syncStatus);

    /// <summary>Marks the scan as accepted by the server.</summary>
    public void MarkSynced() => SyncStatus = SyncStatus.Synced;

    /// <summary>Marks the last upload attempt as failed so the scan is retried later.</summary>
    public void MarkSyncFailed() => SyncStatus = SyncStatus.Failed;

    private static string Required(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim();
    }

    private static string Matching(string value, Func<string, bool> isValid, string pattern, string paramName) =>
        isValid(value)
            ? value
            : throw new ArgumentException($"'{value}' does not match the format {pattern}.", paramName);

    private static T Defined<T>(T value, string paramName)
        where T : struct, Enum =>
        Enum.IsDefined(value)
            ? value
            : throw new ArgumentException($"Unknown {typeof(T).Name} value {value}.", paramName);
}
