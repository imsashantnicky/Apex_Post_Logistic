using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Tests.Support;

namespace ApexPost.Courier.Offline.Tests.Queries;

public sealed class ScanQueryTests : IDisposable
{
    private readonly TempCourierDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task SaveAsync(params ShipmentScan[] scans)
    {
        foreach (var scan in scans)
        {
            await _db.Repository.CreateAsync(scan, Ct);
        }
    }

    [Fact]
    public async Task Find_by_tracking_number_returns_only_that_shipment_oldest_first()
    {
        // Saved out of time order on purpose.
        var late = TestScans.At(30, scanType: ScanType.Delivered);
        var early = TestScans.At(0);
        var middle = TestScans.At(10, scanType: ScanType.InTransit);
        await SaveAsync(late, early, middle, TestScans.At(5, trackingNumber: "APX-10000002"));

        var scans = await _db.Repository.GetByTrackingNumberAsync("APX-10000001", Ct);

        Assert.Equal([early.Id, middle.Id, late.Id], scans.Select(scan => scan.Id));
    }

    [Fact]
    public async Task Pending_query_returns_only_pending_scans_oldest_first()
    {
        var second = TestScans.At(20);
        var first = TestScans.At(10);
        await SaveAsync(
            second, first,
            TestScans.At(0, syncStatus: SyncStatus.Synced),
            TestScans.At(5, syncStatus: SyncStatus.Failed));

        var pending = await _db.Repository.GetPendingAsync(Ct);

        Assert.Equal([first.Id, second.Id], pending.Select(scan => scan.Id));
        Assert.Equal(2, await _db.Repository.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task Counts_per_scan_type_cover_only_the_requested_courier()
    {
        await SaveAsync(
            TestScans.At(0, scanType: ScanType.Pickup),
            TestScans.At(1, scanType: ScanType.Pickup),
            TestScans.At(2, scanType: ScanType.Delivered),
            TestScans.At(3, scanType: ScanType.Failed),
            TestScans.At(4, scanType: ScanType.Pickup, courierId: "CR-102"));

        var counts = await _db.Repository.CountByScanTypeAsync("CR-101", Ct);

        Assert.Equal(
            [new ScanTypeCount(ScanType.Delivered, 1), new ScanTypeCount(ScanType.Failed, 1),
                new ScanTypeCount(ScanType.Pickup, 2)],
            counts);
    }

    [Theory]
    [InlineData("APX-10000001' OR '1'='1")]
    [InlineData("APX-10000001\" OR \"1\"=\"1")]
    [InlineData("' OR 1=1 --")]
    [InlineData("APX-10000001 OR trackingNumber IS NOT MISSING")]
    [InlineData("$trackingNumber")]
    public async Task Injection_style_tracking_number_is_a_plain_value_and_matches_nothing(string input)
    {
        await SaveAsync(TestScans.At(0), TestScans.At(1, trackingNumber: "APX-10000002"));

        Assert.Empty(await _db.Repository.GetByTrackingNumberAsync(input, Ct));
    }

    [Fact]
    public async Task Injection_style_courier_id_counts_nothing()
    {
        await SaveAsync(TestScans.At(0), TestScans.At(1, courierId: "CR-102"));

        Assert.Empty(await _db.Repository.CountByScanTypeAsync("CR-101' OR '1'='1", Ct));
    }

    public void Dispose() => _db.Dispose();
}
