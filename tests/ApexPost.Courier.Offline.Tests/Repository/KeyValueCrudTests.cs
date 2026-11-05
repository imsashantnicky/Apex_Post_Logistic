using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.Offline.Tests.Support;

namespace ApexPost.Courier.Offline.Tests.Repository;

public sealed class KeyValueCrudTests : IDisposable
{
    private readonly TempCourierDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_read_update_delete_round_trip_by_document_id()
    {
        var repository = _db.Repository;
        var scan = ShipmentScan.Restore(
            ScanIds.New(), "APX-10000002", ScanType.InTransit, "CR-102", "North Hub", TestScans.Start,
            "Fragile", SyncStatus.Synced);

        await repository.CreateAsync(scan, Ct);
        var created = await repository.GetByIdAsync(scan.Id, Ct);
        Assert.NotNull(created);
        Assert.Equal(
            (scan.Id, scan.TrackingNumber, scan.ScanType, scan.CourierId, scan.Location, scan.ScannedAtUtc, scan.Notes),
            (created.Id, created.TrackingNumber, created.ScanType, created.CourierId, created.Location,
                created.ScannedAtUtc, created.Notes));
        Assert.Equal(SyncStatus.Synced, created.SyncStatus);

        var edited = ShipmentScan.Restore(
            scan.Id, scan.TrackingNumber, ScanType.Delivered, scan.CourierId, "Maple Depot", scan.ScannedAtUtc,
            "Left with reception", scan.SyncStatus);
        await repository.UpdateAsync(edited, Ct);
        var updated = await repository.GetByIdAsync(scan.Id, Ct);
        Assert.Equal(
            (ScanType.Delivered, "Maple Depot", "Left with reception", SyncStatus.Pending),
            (updated?.ScanType, updated?.Location, updated?.Notes, updated?.SyncStatus));

        Assert.True(await repository.DeleteAsync(scan.Id, Ct));
        Assert.Null(await repository.GetByIdAsync(scan.Id, Ct));
        Assert.False(await repository.DeleteAsync(scan.Id, Ct));
    }

    [Fact]
    public async Task A_second_create_with_the_same_id_is_rejected()
    {
        var scan = TestScans.At(0);
        await _db.Repository.CreateAsync(scan, Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.Repository.CreateAsync(scan, Ct));
    }

    [Fact]
    public async Task Get_by_an_unknown_or_malformed_id_returns_null()
    {
        Assert.Null(await _db.Repository.GetByIdAsync(ScanIds.New(), Ct));
        Assert.Null(await _db.Repository.GetByIdAsync("not-a-scan-id", Ct));
    }

    [Fact]
    public async Task Data_survives_closing_and_reopening_the_database()
    {
        var scan = TestScans.At(0);
        await _db.Repository.CreateAsync(scan, Ct);
        _db.Database.Dispose();

        using var reopened = new CourierDatabase(_db.DatabaseFolder);
        var stored = await new CouchbaseScanRepository(reopened).GetByIdAsync(scan.Id, Ct);

        Assert.Equal(scan.TrackingNumber, stored?.TrackingNumber);
    }

    public void Dispose() => _db.Dispose();
}
