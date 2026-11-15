using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Scans;

namespace ApexPost.Courier.Offline.Tests.Queries;

/// <summary>
/// SQL++ queries with parameters and value indexes on a real embedded Couchbase Lite database. Week 5 step 02 moved
/// this class here from ApexPost.Courier.UnitTests and merged the unique cases of ScanQueryTests and IndexTests.
/// </summary>
[Trait("Category", "Integration")]
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

    [Fact]
    public async Task GetByTrackingNumberAsync_MixedShipments_ReturnsOnlyThatShipmentOldestFirst()
    {
        // Arrange
        var delivered = await AddAsync(type: ScanType.Delivered, minutes: 120);
        var pickup = await AddAsync(type: ScanType.Pickup, minutes: 0);
        await AddAsync(trackingNumber: Tracking2);
        var transit = await AddAsync(type: ScanType.InTransit, minutes: 45);

        // Act
        var scans = await _repository.GetByTrackingNumberAsync(Tracking1, Ct);

        // Assert
        Assert.Equal([pickup.Id, transit.Id, delivered.Id], scans.Select(s => s.Id));
    }

    [Fact]
    public async Task GetByTrackingNumberAsync_LowerCaseInput_FindsTheScan()
    {
        // Arrange
        await AddAsync();

        // Act
        var scans = await _repository.GetByTrackingNumberAsync(" apx-10000001 ", Ct);

        // Assert
        Assert.Single(scans);
    }

    [Theory]
    [InlineData("APX-00000000' OR '1'='1")]
    [InlineData("APX-10000001' OR '1'='1")]
    [InlineData("APX-10000001\" OR \"1\"=\"1")]
    [InlineData("' OR 1=1 --")]
    [InlineData("APX-10000001 OR trackingNumber IS NOT MISSING")]
    [InlineData("$trackingNumber")]
    public async Task GetByTrackingNumberAsync_InjectionStyleInput_IsAPlainValueAndMatchesNothing(string input)
    {
        // Arrange
        await AddAsync();
        await AddAsync(trackingNumber: Tracking2);

        // Act: bound as $trackingNumber, so quotes and OR are part of the compared string, not query syntax.
        var scans = await _repository.GetByTrackingNumberAsync(input, Ct);

        // Assert
        Assert.Empty(scans);
    }

    [Fact]
    public async Task GetPendingAsync_MixedStatuses_ReturnsOnlyPendingOldestFirst()
    {
        // Arrange
        var later = await AddAsync(minutes: 30);
        await AddAsync(minutes: 10, status: SyncStatus.Synced);
        await AddAsync(minutes: 20, status: SyncStatus.Failed);
        var earlier = await AddAsync(minutes: 5);

        // Act
        var pending = await _repository.GetPendingAsync(Ct);

        // Assert
        Assert.Equal([earlier.Id, later.Id], pending.Select(s => s.Id));
        Assert.All(pending, s => Assert.Equal(SyncStatus.Pending, s.SyncStatus));
    }

    [Fact]
    public async Task GetFailedAsync_MixedStatuses_ReturnsOnlyFailedOldestFirst()
    {
        // Arrange
        var later = await AddAsync(minutes: 30, status: SyncStatus.Failed);
        await AddAsync(minutes: 10);
        await AddAsync(minutes: 20, status: SyncStatus.Synced);
        var earlier = await AddAsync(minutes: 5, status: SyncStatus.Failed);

        // Act
        var failed = await _repository.GetFailedAsync(Ct);

        // Assert
        Assert.Equal([earlier.Id, later.Id], failed.Select(s => s.Id));
    }

    [Fact]
    public async Task GetPendingAsync_ProjectedScan_RestoresEveryField()
    {
        // Arrange
        var scan = await AddAsync(type: ScanType.InTransit, courierId: "CR-102", minutes: 15);

        // Act
        var loaded = Assert.Single(await _repository.GetPendingAsync(Ct));

        // Assert
        Assert.Equal(scan.TrackingNumber, loaded.TrackingNumber);
        Assert.Equal(scan.ScanType, loaded.ScanType);
        Assert.Equal(scan.CourierId, loaded.CourierId);
        Assert.Equal(scan.Location, loaded.Location);
        Assert.Equal(scan.ScannedAtUtc, loaded.ScannedAtUtc);
    }

    [Fact]
    public async Task CountByScanTypeAsync_TwoCouriers_GroupsOnlyTheRequestedCourierByTypeName()
    {
        // Arrange
        await AddAsync(type: ScanType.Pickup);
        await AddAsync(type: ScanType.Pickup);
        await AddAsync(type: ScanType.Delivered);
        await AddAsync(type: ScanType.Failed, courierId: "CR-102");

        // Act
        var counts = await _repository.CountByScanTypeAsync("CR-101", Ct);

        // Assert
        Assert.Equal([new ScanTypeCount(ScanType.Delivered, 1), new ScanTypeCount(ScanType.Pickup, 2)], counts);
    }

    [Fact]
    public async Task CountByScanTypeAsync_UnknownCourier_ReturnsEmpty()
    {
        // Arrange
        await AddAsync();

        // Act
        var counts = await _repository.CountByScanTypeAsync("CR-999", Ct);

        // Assert
        Assert.Empty(counts);
    }

    [Fact]
    public async Task CountByScanTypeAsync_InjectionStyleCourierId_CountsNothing()
    {
        // Arrange
        await AddAsync();
        await AddAsync(courierId: "CR-102");

        // Act
        var counts = await _repository.CountByScanTypeAsync("CR-101' OR '1'='1", Ct);

        // Assert
        Assert.Empty(counts);
    }

    [Fact]
    public async Task CountPendingAsync_SyncStatusChanges_FollowsTheChange()
    {
        // Arrange
        var before = await _repository.CountPendingAsync(Ct);
        var first = await AddAsync();
        await AddAsync();
        await AddAsync(status: SyncStatus.Synced);
        var afterAdding = await _repository.CountPendingAsync(Ct);

        // Act
        await _repository.UpdateSyncStatusAsync(first.Id, SyncStatus.Synced, Ct);
        var afterSync = await _repository.CountPendingAsync(Ct);

        // Assert
        Assert.Equal(0, before);
        Assert.Equal(2, afterAdding);
        Assert.Equal(1, afterSync);
    }

    [Fact]
    public void ExplainQueries_BothQueries_UseTheirValueIndexes()
    {
        // Arrange
        var repository = _repository;

        // Act
        var plans = repository.ExplainQueries();
        foreach (var (name, plan) in plans)
        {
            TestContext.Current.TestOutputHelper?.WriteLine($"{name}:{Environment.NewLine}{plan}");
        }

        // Assert
        Assert.Contains(
            CourierDatabase.TrackingNumberIndexName,
            plans[nameof(CouchbaseScanRepository.GetByTrackingNumberAsync)],
            StringComparison.Ordinal);
        Assert.Contains(
            CourierDatabase.SyncStatusIndexName,
            plans[nameof(CouchbaseScanRepository.GetPendingAsync)],
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(CouchbaseScanRepository.GetByTrackingNumberAsync), CourierDatabase.TrackingNumberIndexName)]
    [InlineData(nameof(CouchbaseScanRepository.GetPendingAsync), CourierDatabase.SyncStatusIndexName)]
    public void ExplainQueries_EachQuery_SearchesUsingItsIndex(string method, string indexName)
    {
        // Arrange
        var repository = _repository;

        // Act
        var plans = repository.ExplainQueries();

        // Assert
        Assert.Contains($"SEARCH _ USING INDEX {indexName}", plans[method], StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _database.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

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
}
