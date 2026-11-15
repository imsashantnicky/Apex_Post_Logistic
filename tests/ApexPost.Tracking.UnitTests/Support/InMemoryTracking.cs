using ApexPost.Application.DTOs;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;

namespace ApexPost.Tracking.UnitTests.Support;

/// <summary>
/// The three tracking ports in memory. Additions and removals are staged until SaveChangesAsync, like a unit of work;
/// <see cref="BeforeNextSave"/> lets a test fail one commit, as a parallel upload would.
/// </summary>
internal sealed class InMemoryTracking : ITrackedShipmentRepository, ITrackingQueries, ITrackingUnitOfWork
{
    private readonly Dictionary<string, TrackedShipment> _shipments = new(StringComparer.Ordinal);
    private readonly List<TrackingScan> _scans = [];
    private readonly List<TrackedShipment> _addedShipments = [];
    private readonly List<TrackedShipment> _removedShipments = [];
    private readonly List<TrackingScan> _addedScans = [];

    public int Saves { get; private set; }

    public int Discards { get; private set; }

    /// <summary>Runs once at the start of the next commit; throwing from it fails that commit.</summary>
    public Action? BeforeNextSave { get; set; }

    public IReadOnlyList<TrackingScan> StoredScans => _scans;

    public void Store(TrackedShipment shipment) => _shipments[shipment.TrackingNumber] = shipment;

    public void StoreScan(TrackingScan scan) => _scans.Add(scan);

    public TrackedShipment? Stored(string trackingNumber) => _shipments.GetValueOrDefault(trackingNumber);

    public Task<TrackedShipment?> FindAsync(string trackingNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Stored(trackingNumber));

    public Task<IReadOnlySet<string>> FindStoredScanIdsAsync(
        IReadOnlyCollection<string> scanIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(
            _scans.Select(scan => scan.ScanId).Where(scanIds.Contains).ToHashSet(StringComparer.Ordinal));

    public void Add(TrackedShipment shipment) => _addedShipments.Add(shipment);

    public void Remove(TrackedShipment shipment) => _removedShipments.Add(shipment);

    public void AddScan(TrackingScan scan) => _addedScans.Add(scan);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var hook = BeforeNextSave;
        BeforeNextSave = null;
        hook?.Invoke();
        _addedShipments.ForEach(Store);
        _removedShipments.ForEach(shipment => _shipments.Remove(shipment.TrackingNumber));
        _scans.AddRange(_addedScans);
        ClearStaged();
        Saves++;
        return Task.CompletedTask;
    }

    public void DiscardChanges()
    {
        ClearStaged();
        Discards++;
    }

    public Task<PagedResult<ShipmentView>> ListAsync(ShipmentListFilter filter, CancellationToken cancellationToken)
    {
        var matching = _shipments.Values
            .Where(shipment => filter.Status is null || shipment.Status == filter.Status)
            .Where(shipment => filter.City is null
                || string.Equals(shipment.Recipient.City, filter.City, StringComparison.OrdinalIgnoreCase))
            .OrderBy(shipment => shipment.TrackingNumber, StringComparer.Ordinal)
            .ToList();
        var page = matching.Skip(filter.Skip).Take(filter.PageSize).Select(View).ToList();
        return Task.FromResult(new PagedResult<ShipmentView>(page, filter.Page, filter.PageSize, matching.Count));
    }

    // The repository has a FindAsync of its own, so this one is reached through the queries port.
    Task<ShipmentView?> ITrackingQueries.FindAsync(string trackingNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Stored(trackingNumber) is { } shipment ? View(shipment) : null);

    public Task<IReadOnlyList<ScanView>?> FindScansAsync(string trackingNumber, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScanView>?>(
            Stored(trackingNumber) is { } shipment ? ScansOf(shipment).ToList() : null);

    private IEnumerable<ScanView> ScansOf(TrackedShipment shipment) =>
        _scans.Where(scan => scan.ShipmentId == shipment.Id)
            .OrderBy(scan => scan.ScannedAtUtc)
            .Select(scan => new ScanView(
                scan.ScanId,
                shipment.TrackingNumber,
                scan.ScanType,
                scan.CourierId,
                scan.Location,
                scan.ScannedAtUtc,
                scan.Notes,
                scan.ReceivedAtUtc));

    private ShipmentView View(TrackedShipment shipment) => new(
        shipment.TrackingNumber,
        shipment.Recipient.Name,
        shipment.Recipient.Phone,
        shipment.Recipient.Address,
        shipment.Recipient.City,
        shipment.WeightKg,
        shipment.Status,
        shipment.CreatedAtUtc,
        shipment.Version,
        ScansOf(shipment).LastOrDefault());

    private void ClearStaged()
    {
        _addedShipments.Clear();
        _removedShipments.Clear();
        _addedScans.Clear();
    }
}
