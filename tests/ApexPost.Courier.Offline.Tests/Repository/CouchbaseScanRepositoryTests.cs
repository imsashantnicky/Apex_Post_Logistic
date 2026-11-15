using System.Reflection;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Scans;

namespace ApexPost.Courier.Offline.Tests.Repository;

/// <summary>
/// Key-value CRUD of the scan repository on a real embedded Couchbase Lite database in a unique temp folder. Week 5
/// step 02 moved this class here from ApexPost.Courier.UnitTests and merged KeyValueCrudTests into it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CouchbaseScanRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset BaseTime = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "apexpost-courier-tests", Guid.NewGuid().ToString("N"));

    private readonly CourierDatabase _database;
    private readonly CouchbaseScanRepository _repository;

    public CouchbaseScanRepositoryTests()
    {
        _database = new CourierDatabase(_directory);
        _repository = new CouchbaseScanRepository(_database);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetByIdAsync_AfterCreate_ReturnsTheSameScan()
    {
        // Arrange
        var scan = Scan();
        await _repository.CreateAsync(scan, Ct);

        // Act
        var loaded = await _repository.GetByIdAsync(scan.Id, Ct);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(scan.Id, loaded.Id);
        Assert.Equal(scan.ScannedAtUtc, loaded.ScannedAtUtc);
        Assert.Equal(SyncStatus.Pending, loaded.SyncStatus);
    }

    [Fact]
    public async Task CreateAsync_ExistingId_ThrowsAndKeepsTheOriginal()
    {
        // Arrange
        var original = Scan(location: "BR001");
        await _repository.CreateAsync(original, Ct);

        // Act
        var create = () => _repository.CreateAsync(Scan(location: "BR002", id: original.Id), Ct);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(create);
        Assert.Equal("BR001", (await _repository.GetByIdAsync(original.Id, Ct))!.Location);
    }

    [Theory]
    [InlineData("scan::not-a-guid")]
    [InlineData("doc::1")]
    public async Task GetByIdAsync_InvalidId_ReturnsNull(string id)
    {
        // Arrange
        await _repository.CreateAsync(Scan(), Ct);

        // Act
        var loaded = await _repository.GetByIdAsync(id, Ct);

        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        var unknownId = ScanIds.New();

        // Act
        var loaded = await _repository.GetByIdAsync(unknownId, Ct);

        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task UpdateAsync_SyncedScan_SavesChangesAndPutsItBackToPending()
    {
        // Arrange
        var scan = Scan(status: SyncStatus.Synced);
        await _repository.CreateAsync(scan, Ct);

        // Act
        await _repository.UpdateAsync(Scan(location: "Harbour Point", status: SyncStatus.Synced, id: scan.Id), Ct);

        // Assert
        var loaded = await _repository.GetByIdAsync(scan.Id, Ct);
        Assert.Equal("Harbour Point", loaded!.Location);
        Assert.Equal(SyncStatus.Pending, loaded.SyncStatus);
    }

    [Fact]
    public async Task UpdateAsync_MissingScan_ThrowsKeyNotFoundException()
    {
        // Arrange
        var missing = Scan();

        // Act
        var update = () => _repository.UpdateAsync(missing, Ct);

        // Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(update);
    }

    [Fact]
    public async Task DeleteAsync_CalledTwice_ReturnsTrueThenFalse()
    {
        // Arrange
        var scan = Scan();
        await _repository.CreateAsync(scan, Ct);

        // Act
        var first = await _repository.DeleteAsync(scan.Id, Ct);
        var second = await _repository.DeleteAsync(scan.Id, Ct);

        // Assert
        Assert.True(first);
        Assert.False(second);
        Assert.Null(await _repository.GetByIdAsync(scan.Id, Ct));
    }

    [Fact]
    public async Task ListAllAsync_ScansSavedOutOfOrder_ReturnsThemOrderedByScannedAtUtc()
    {
        // Arrange
        var late = Scan(minutes: 30);
        var early = Scan(minutes: -90);
        var middle = Scan(minutes: 5);
        foreach (var scan in new[] { late, early, middle })
        {
            await _repository.CreateAsync(scan, Ct);
        }

        // Act
        var all = await _repository.ListAllAsync(Ct);

        // Assert
        Assert.Equal([early.Id, middle.Id, late.Id], all.Select(s => s.Id));
    }

    [Fact]
    public async Task ListAllAsync_DocumentOfAnotherType_IsIgnored()
    {
        // Arrange
        await _repository.CreateAsync(Scan(), Ct);
        using (var other = new Couchbase.Lite.MutableDocument("note::1"))
        {
            other.SetString("type", "note");
            _database.GetDefaultCollection().Save(other);
        }

        // Act
        var all = await _repository.ListAllAsync(Ct);

        // Assert
        Assert.Single(all);
    }

    [Fact]
    public async Task UpdateSyncStatusAsync_ExistingScan_ChangesOnlyTheStatus()
    {
        // Arrange
        var scan = Scan(location: "BR003");
        await _repository.CreateAsync(scan, Ct);

        // Act
        var updated = await _repository.UpdateSyncStatusAsync(scan.Id, SyncStatus.Synced, Ct);

        // Assert
        var loaded = await _repository.GetByIdAsync(scan.Id, Ct);
        Assert.True(updated);
        Assert.Equal(SyncStatus.Synced, loaded!.SyncStatus);
        Assert.Equal("BR003", loaded.Location);
    }

    [Fact]
    public async Task UpdateSyncStatusAsync_MissingScan_ReturnsFalse()
    {
        // Arrange
        var unknownId = ScanIds.New();

        // Act
        var updated = await _repository.UpdateSyncStatusAsync(unknownId, SyncStatus.Failed, Ct);

        // Assert
        Assert.False(updated);
    }

    [Fact]
    public async Task CreateAsync_CancelledToken_ThrowsBeforeAnyWork()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var create = () => _repository.CreateAsync(Scan(), cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(create);
        Assert.Equal(0UL, _database.GetDefaultCollection().Count);
    }

    [Fact]
    public async Task GetByIdAsync_AfterClosingAndReopeningTheDatabase_ReturnsTheStoredScan()
    {
        // Arrange
        var scan = Scan();
        await _repository.CreateAsync(scan, Ct);
        _database.Dispose();

        // Act
        using var reopened = new CourierDatabase(_directory);
        var stored = await new CouchbaseScanRepository(reopened).GetByIdAsync(scan.Id, Ct);

        // Assert
        Assert.Equal(scan.TrackingNumber, stored?.TrackingNumber);
    }

    [Fact]
    public void PublicMembers_OfTheRepository_ExposeNoCouchbaseTypes()
    {
        // Arrange
        var members = typeof(CouchbaseScanRepository)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

        // Act
        var types = members.SelectMany(TypesOf);

        // Assert
        Assert.DoesNotContain(types, t => t.Namespace?.StartsWith("Couchbase", StringComparison.Ordinal) == true);
    }

    public void Dispose()
    {
        _database.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static ShipmentScan Scan(
        int minutes = 0, SyncStatus status = SyncStatus.Pending, string location = "BR001", string? id = null) =>
        ShipmentScan.Restore(
            id ?? ScanIds.New(), "APX-10000001", ScanType.Pickup, "CR-101", location,
            BaseTime.AddMinutes(minutes), null, status);

    private static IEnumerable<Type> TypesOf(MemberInfo member) => member switch
    {
        MethodBase method => method.GetParameters().Select(p => p.ParameterType)
            .Concat(method is MethodInfo info ? [info.ReturnType] : []).SelectMany(Flatten),
        PropertyInfo property => Flatten(property.PropertyType),
        _ => [],
    };

    private static IEnumerable<Type> Flatten(Type type) =>
        type.IsGenericType ? [type, .. type.GetGenericArguments().SelectMany(Flatten)] : [type];
}
