using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Tracking.UnitTests.Support;

namespace ApexPost.Tracking.UnitTests.Services;

/// <summary>
/// A courier may upload only the scans it recorded: the courierId of every scan must equal the courier of the
/// request (the token claim in the API), or the whole batch is refused with 403 and nothing is stored.
/// </summary>
public sealed class ScanOwnershipTests
{
    private const string OtherCourier = "CR-102";

    private readonly InMemoryTracking _store = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_courier_of_every_scan_may_upload_them()
    {
        _store.Store(TestData.Shipment());
        var service = ServiceFor(TestData.CourierId);
        ScanUpload[] batch = [TestData.Scan(ScanType.Pickup), TestData.Scan(ScanType.InTransit, minutesAfterNow: 5)];

        var result = await service.UploadShipmentScansAsync(TestData.TrackingNumber, batch, Ct);

        Assert.Equal(2, result.Accepted);
        Assert.Equal(ShipmentStatus.InTransit, _store.Stored(TestData.TrackingNumber)!.Status);
    }

    [Fact]
    public async Task A_scan_of_another_courier_refuses_the_whole_shipment_upload_and_stores_nothing()
    {
        _store.Store(TestData.Shipment());
        var service = ServiceFor(TestData.CourierId);
        var foreign = TestData.Scan(ScanType.InTransit, minutesAfterNow: 5, courierId: OtherCourier);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => service.UploadShipmentScansAsync(
            TestData.TrackingNumber,
            [TestData.Scan(ScanType.Pickup), foreign],
            Ct));

        Assert.Contains(foreign.ScanId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(OtherCourier, exception.Message, StringComparison.Ordinal);
        Assert.Empty(_store.StoredScans);
        Assert.Equal(ShipmentStatus.Created, _store.Stored(TestData.TrackingNumber)!.Status);
    }

    [Fact]
    public async Task A_scan_of_another_courier_refuses_a_mixed_device_upload_as_well()
    {
        _store.Store(TestData.Shipment());
        var service = ServiceFor(OtherCourier);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UploadScansAsync([TestData.Scan(ScanType.Pickup)], Ct));

        Assert.Empty(_store.StoredScans);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CR-1")]
    public async Task A_caller_without_a_valid_courier_id_cannot_upload(string? callerCourierId)
    {
        _store.Store(TestData.Shipment());
        var service = ServiceFor(callerCourierId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UploadShipmentScansAsync(TestData.TrackingNumber, [TestData.Scan(ScanType.Pickup)], Ct));

        Assert.Empty(_store.StoredScans);
    }

    [Fact]
    public async Task Ownership_is_checked_before_the_shipment_is_looked_up()
    {
        var service = ServiceFor(TestData.CourierId);
        var foreign = TestData.Scan(ScanType.Pickup, TestData.OtherTrackingNumber, courierId: OtherCourier);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UploadShipmentScansAsync(TestData.OtherTrackingNumber, [foreign], Ct));
    }

    [Fact]
    public async Task A_malformed_courier_id_claims_no_owner_and_is_rejected_per_scan()
    {
        _store.Store(TestData.Shipment());
        var service = ServiceFor(TestData.CourierId);
        var malformed = TestData.Scan(ScanType.Pickup, courierId: "CR-1");
        var good = TestData.Scan(ScanType.Pickup);

        var result = await service.UploadShipmentScansAsync(TestData.TrackingNumber, [malformed, good], Ct);

        Assert.Equal(1, result.Accepted);
        Assert.Equal([malformed.ScanId], result.Rejected.Select(scan => scan.ScanId));
    }

    private ShipmentTrackingService ServiceFor(string? courierId) =>
        new(_store, _store, _store, new FixedCourier(courierId), new FixedTimeProvider());
}
