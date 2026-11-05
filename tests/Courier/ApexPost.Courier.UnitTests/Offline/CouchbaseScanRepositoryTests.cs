using System.Reflection;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Scans;

namespace ApexPost.Courier.UnitTests.Offline;

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

    private static ShipmentScan Scan(
        int minutes = 0, SyncStatus status = SyncStatus.Pending, string location = "BR001", string? id = null) =>
        ShipmentScan.Restore(
            id ?? ScanIds.New(), "APX-10000001", ScanType.Pickup, "CR-101", location,
            BaseTime.AddMinutes(minutes), null, status);

    [Fact]
    public async Task Create_then_GetById_returns_the_same_scan()
    {
        var scan = Scan();

        await _repository.CreateAsync(scan, Ct);
        var loaded = await _repository.GetByIdAsync(scan.Id, Ct);

        Assert.NotNull(loaded);
        Assert.Equal(scan.Id, loaded.Id);
        Assert.Equal(scan.ScannedAtUtc, loaded.ScannedAtUtc);
        Assert.Equal(SyncStatus.Pending, loaded.SyncStatus);
    }

    [Fact]
    public async Task Create_with_an_existing_id_is_rejected_and_keeps_the_original()
    {
        var original = Scan(location: "BR001");
        await _repository.CreateAsync(original, Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.CreateAsync(Scan(location: "BR002", id: original.Id), Ct));

        Assert.Equal("BR001", (await _repository.GetByIdAsync(original.Id, Ct))!.Location);
    }

    [Theory]
    [InlineData("scan::not-a-guid")]
    [InlineData("doc::1")]
    public async Task GetById_with_an_invalid_id_returns_null(string id)
    {
        Assert.Null(await _repository.GetByIdAsync(id, Ct));
    }

    [Fact]
    public async Task GetById_for_an_unknown_id_returns_null()
    {
        Assert.Null(await _repository.GetByIdAsync(ScanIds.New(), Ct));
    }

    [Fact]
    public async Task Update_saves_changes_and_puts_a_synced_scan_back_to_pending()
    {
        var scan = Scan(status: SyncStatus.Synced);
        await _repository.CreateAsync(scan, Ct);

        await _repository.UpdateAsync(Scan(location: "Harbour Point", status: SyncStatus.Synced, id: scan.Id), Ct);
        var loaded = await _repository.GetByIdAsync(scan.Id, Ct);

        Assert.Equal("Harbour Point", loaded!.Location);
        Assert.Equal(SyncStatus.Pending, loaded.SyncStatus);
    }

    [Fact]
    public async Task Update_of_a_missing_scan_throws_KeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _repository.UpdateAsync(Scan(), Ct));
    }

    [Fact]
    public async Task Delete_returns_true_once_then_false()
    {
        var scan = Scan();
        await _repository.CreateAsync(scan, Ct);

        Assert.True(await _repository.DeleteAsync(scan.Id, Ct));
        Assert.False(await _repository.DeleteAsync(scan.Id, Ct));
        Assert.Null(await _repository.GetByIdAsync(scan.Id, Ct));
    }

    [Fact]
    public async Task ListAll_returns_scans_ordered_by_scannedAtUtc()
    {
        var late = Scan(minutes: 30);
        var early = Scan(minutes: -90);
        var middle = Scan(minutes: 5);
        foreach (var scan in new[] { late, early, middle })
        {
            await _repository.CreateAsync(scan, Ct);
        }

        var all = await _repository.ListAllAsync(Ct);

        Assert.Equal([early.Id, middle.Id, late.Id], all.Select(s => s.Id));
    }

    [Fact]
    public async Task ListAll_ignores_documents_of_another_type()
    {
        await _repository.CreateAsync(Scan(), Ct);
        using (var other = new Couchbase.Lite.MutableDocument("note::1"))
        {
            other.SetString("type", "note");
            _database.GetDefaultCollection().Save(other);
        }

        Assert.Single(await _repository.ListAllAsync(Ct));
    }

    [Fact]
    public async Task UpdateSyncStatus_changes_only_the_status()
    {
        var scan = Scan(location: "BR003");
        await _repository.CreateAsync(scan, Ct);

        Assert.True(await _repository.UpdateSyncStatusAsync(scan.Id, SyncStatus.Synced, Ct));
        var loaded = await _repository.GetByIdAsync(scan.Id, Ct);

        Assert.Equal(SyncStatus.Synced, loaded!.SyncStatus);
        Assert.Equal("BR003", loaded.Location);
    }

    [Fact]
    public async Task UpdateSyncStatus_of_a_missing_scan_returns_false()
    {
        Assert.False(await _repository.UpdateSyncStatusAsync(ScanIds.New(), SyncStatus.Failed, Ct));
    }

    [Fact]
    public async Task Cancelled_token_stops_before_any_work()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _repository.CreateAsync(Scan(), cts.Token));
        Assert.Equal(0UL, _database.GetDefaultCollection().Count);
    }

    [Fact]
    public void Public_members_expose_no_Couchbase_types()
    {
        var members = typeof(CouchbaseScanRepository)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        var types = members.SelectMany(TypesOf);

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
