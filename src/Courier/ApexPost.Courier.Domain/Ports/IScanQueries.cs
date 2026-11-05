using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.Domain.Ports;

/// <summary>Read-only questions about the scans stored on the device.</summary>
public interface IScanQueries
{
    /// <summary>Returns the scans of one shipment, oldest first by <see cref="ShipmentScan.ScannedAtUtc"/>.</summary>
    Task<IReadOnlyList<ShipmentScan>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken ct);

    /// <summary>Returns the scans that still have to be uploaded.</summary>
    Task<IReadOnlyList<ShipmentScan>> GetPendingAsync(CancellationToken ct);

    /// <summary>Returns how many scans of each type one courier has recorded.</summary>
    Task<IReadOnlyList<ScanTypeCount>> CountByScanTypeAsync(string courierId, CancellationToken ct);

    /// <summary>Returns how many scans still have to be uploaded.</summary>
    Task<int> CountPendingAsync(CancellationToken ct);
}
