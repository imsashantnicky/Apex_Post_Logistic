using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Scans;

namespace ApexPost.Courier.UnitTests.Offline;

public sealed class CouchbaseScanQueriesTests : IDisposable
{
    private const string Tracking1 = "APX-10000001";
    private const string Tracking2 = "APX-10000002";

    private static readonly DateTimeOffset BaseTime = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "apexpost-courier-tests", Guid.NewGuid().ToString("N"));

    private readonly CourierDatabase _database;
    private readonly CouchbaseScanRepository _repository;

    public CouchbaseScanQueriesTests()
    {
        _database = new CourierDatabase(_directory);
        _repository = new CouchbaseScanRepository(_database);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<ShipmentScan> AddAsync(
        string trackingNumber = Tracking1,
        ScanType type = ScanType.Pickup,
        string courierId = "CR-101",
        int minutes = 0,
        SyncStatus status = SyncStatus.Pending)
    {
        var scan = ShipmentScan.Restore(
            ScanIds.New(), trackingNumber, type, courierId, "BR001", BaseTime.AddMinutes(minutes), null, status);
        await _repository.CreateAsync(scan, Ct);
        return scan;
    }

    [Fact]
    public async Task GetByTrackingNumber_returns_only_that_shipment_oldest_first()
    {
        var delivered = await AddAsync(type: ScanType.Delivered, minutes: 120);
        var pickup = await AddAsync(type: ScanType.Pickup, minutes: 0);
        await AddAsync(trackingNumber: Tracking2);
        var transit = await AddAsync(type: ScanType.InTransit, minutes: 45);

        var scans = await _repository.GetByTrackingNumberAsync(Tracking1, Ct);

        Assert.Equal([pickup.Id, transit.Id, delivered.Id], scans.Select(s => s.Id));
    }

    [Fact]
    public async Task GetByTrackingNumber_accepts_lower_case_input()
    {
        await AddAsync();

        Assert.Single(await _repository.GetByTrackingNumberAsync(" apx-10000001 ", Ct));
    }

    [Fact]
    public async Task GetByTrackingNumber_treats_an_injection_attempt_as_a_plain_value()
    {
        await AddAsync();
        await AddAsync(trackingNumber: Tracking2);

        // Bound as $trackingNumber, so the quote and OR are part of the compared string, not query syntax.
        var scans = await _repository.GetByTrackingNumberAsync("APX-00000000' OR '1'='1", Ct);

        Assert.Empty(scans);
    }

    [Fact]
    public async Task GetPending_returns_only_pending_scans_oldest_first()
    {
        var later = await AddAsync(minutes: 30);
        await AddAsync(minutes: 10, status: SyncStatus.Synced);
        await AddAsync(minutes: 20, status: SyncStatus.Failed);
        var earlier = await AddAsync(minutes: 5);

        var pending = await _repository.GetPendingAsync(Ct);

        Assert.Equal([earlier.Id, later.Id], pending.Select(s => s.Id));
        Assert.All(pending, s => Assert.Equal(SyncStatus.Pending, s.SyncStatus));
    }

    [Fact]
    public async Task GetFailed_returns_only_failed_scans_oldest_first()
    {
        var later = await AddAsync(minutes: 30, status: SyncStatus.Failed);
        await AddAsync(minutes: 10);
        await AddAsync(minutes: 20, status: SyncStatus.Synced);
        var earlier = await AddAsync(minutes: 5, status: SyncStatus.Failed);

        var failed = await _repository.GetFailedAsync(Ct);

        Assert.Equal([earlier.Id, later.Id], failed.Select(s => s.Id));
    }

    [Fact]
    public async Task GetPending_restores_every_field_from_the_projection()
    {
        var scan = await AddAsync(type: ScanType.InTransit, courierId: "CR-102", minutes: 15);

        var loaded = Assert.Single(await _repository.GetPendingAsync(Ct));

        Assert.Equal(scan.TrackingNumber, loaded.TrackingNumber);
        Assert.Equal(scan.ScanType, loaded.ScanType);
        Assert.Equal(scan.CourierId, loaded.CourierId);
        Assert.Equal(scan.Location, loaded.Location);
        Assert.Equal(scan.ScannedAtUtc, loaded.ScannedAtUtc);
    }

    [Fact]
    public async Task CountByScanType_groups_one_couriers_scans_by_type_name()
    {
        await AddAsync(type: ScanType.Pickup);
        await AddAsync(type: ScanType.Pickup);
        await AddAsync(type: ScanType.Delivered);
        await AddAsync(type: ScanType.Failed, courierId: "CR-102");

        var counts = await _repository.CountByScanTypeAsync("CR-101", Ct);

        Assert.Equal([new ScanTypeCount(ScanType.Delivered, 1), new ScanTypeCount(ScanType.Pickup, 2)], counts);
    }

    [Fact]
    public async Task CountByScanType_for_an_unknown_courier_is_empty()
    {
        await AddAsync();

        Assert.Empty(await _repository.CountByScanTypeAsync("CR-999", Ct));
    }

    [Fact]
    public async Task CountPending_follows_sync_status_changes()
    {
        Assert.Equal(0, await _repository.CountPendingAsync(Ct));
        var first = await AddAsync();
        await AddAsync();
        await AddAsync(status: SyncStatus.Synced);

        Assert.Equal(2, await _repository.CountPendingAsync(Ct));

        await _repository.UpdateSyncStatusAsync(first.Id, SyncStatus.Synced, Ct);
        Assert.Equal(1, await _repository.CountPendingAsync(Ct));
    }

    [Fact]
    public void ExplainQueries_shows_both_value_indexes_in_use()
    {
        var plans = _repository.ExplainQueries();
        foreach (var (name, plan) in plans)
        {
            TestContext.Current.TestOutputHelper?.WriteLine($"{name}:{Environment.NewLine}{plan}");
        }

        Assert.Contains(
            CourierDatabase.TrackingNumberIndexName,
            plans[nameof(CouchbaseScanRepository.GetByTrackingNumberAsync)],
            StringComparison.Ordinal);
        Assert.Contains(
            CourierDatabase.SyncStatusIndexName,
            plans[nameof(CouchbaseScanRepository.GetPendingAsync)],
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _database.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
