using ApexPost.Domain.Tracking;

namespace ApexPost.Application.Tracking;

/// <summary>
/// Loads tracked shipments for a change and stages new shipments, deletions and scans. It never commits; that is
/// <see cref="ITrackingUnitOfWork"/>.
/// </summary>
public interface ITrackedShipmentRepository
{
    /// <summary>The shipment with this tracking number, tracked for changes, or null.</summary>
    Task<TrackedShipment?> FindAsync(string trackingNumber, CancellationToken cancellationToken);

    /// <summary>Which of the given scan ids are already stored.</summary>
    Task<IReadOnlySet<string>> FindStoredScanIdsAsync(
        IReadOnlyCollection<string> scanIds,
        CancellationToken cancellationToken);

    void Add(TrackedShipment shipment);

    void Remove(TrackedShipment shipment);

    void AddScan(TrackingScan scan);
}
