using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Tracking;

/// <summary>Loads and stages tracked shipments and scans in the scoped <see cref="TrackingDbContext"/>.</summary>
public sealed class TrackedShipmentRepository(TrackingDbContext database) : ITrackedShipmentRepository
{
    public Task<TrackedShipment?> FindAsync(string trackingNumber, CancellationToken cancellationToken) =>
        database.Shipments.SingleOrDefaultAsync(
            shipment => shipment.TrackingNumber == trackingNumber,
            cancellationToken);

    public async Task<IReadOnlySet<string>> FindStoredScanIdsAsync(
        IReadOnlyCollection<string> scanIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scanIds);
        var stored = await database.Scans.AsNoTracking()
            .Where(scan => scanIds.Contains(scan.ScanId))
            .Select(scan => scan.ScanId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return stored.ToHashSet(StringComparer.Ordinal);
    }

    public void Add(TrackedShipment shipment) => database.Shipments.Add(shipment);

    public void Remove(TrackedShipment shipment) => database.Shipments.Remove(shipment);

    public void AddScan(TrackingScan scan) => database.Scans.Add(scan);
}
