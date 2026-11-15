using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.Presentation.Tests.Support;

/// <summary>Scan store and queries over a list; no Couchbase needed.</summary>
public sealed class InMemoryScanStore : IScanRepository, IScanQueries
{
    public List<ShipmentScan> Scans { get; } = [];

    public Task CreateAsync(ShipmentScan scan, CancellationToken ct)
    {
        Scans.Add(scan);
        return Task.CompletedTask;
    }

    public Task<ShipmentScan?> GetByIdAsync(string id, CancellationToken ct) =>
        Task.FromResult(Scans.FirstOrDefault(scan => scan.Id == id));

    public Task UpdateAsync(ShipmentScan scan, CancellationToken ct) => Task.CompletedTask;

    public Task<bool> DeleteAsync(string id, CancellationToken ct) =>
        Task.FromResult(Scans.RemoveAll(scan => scan.Id == id) > 0);

    public Task<IReadOnlyList<ShipmentScan>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ShipmentScan>>(Scans.ToList());

    public Task<IReadOnlyList<ShipmentScan>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ShipmentScan>>(Scans.Where(s => s.TrackingNumber == trackingNumber).ToList());

    public Task<IReadOnlyList<ShipmentScan>> GetPendingAsync(CancellationToken ct) => With(SyncStatus.Pending);

    public Task<IReadOnlyList<ShipmentScan>> GetFailedAsync(CancellationToken ct) => With(SyncStatus.Failed);

    public Task<IReadOnlyList<ScanTypeCount>> CountByScanTypeAsync(string courierId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ScanTypeCount>>([]);

    public Task<int> CountPendingAsync(CancellationToken ct) =>
        Task.FromResult(Scans.Count(scan => scan.SyncStatus == SyncStatus.Pending));

    public ShipmentScan Add(string trackingNumber, SyncStatus status, DateTimeOffset scannedAt)
    {
        var scan = ShipmentScan.Restore(
            ScanIds.New(), trackingNumber, ScanType.Pickup, "CR-101", "Central Hub", scannedAt, null, status);
        Scans.Add(scan);
        return scan;
    }

    private Task<IReadOnlyList<ShipmentScan>> With(SyncStatus status) =>
        Task.FromResult<IReadOnlyList<ShipmentScan>>(Scans.Where(scan => scan.SyncStatus == status).ToList());
}
