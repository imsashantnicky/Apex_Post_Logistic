using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Tracking.UnitTests.Support;

namespace ApexPost.Tracking.UnitTests.Services;

/// <summary>The upload of scans for one shipment, as used by POST /api/shipments/{trackingNumber}/scans.</summary>
public sealed class ShipmentScanUploadTests
{
    private readonly InMemoryTracking _store = new();
    private readonly ShipmentTrackingService _service;

    public ShipmentScanUploadTests()
    {
        _service = new ShipmentTrackingService(_store, _store, _store, new FixedCourier(), new FixedTimeProvider());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Scans_of_the_shipment_are_stored_and_a_repeat_is_skipped()
    {
        _store.Store(TestData.Shipment());
        ScanUpload[] batch = [TestData.Scan(ScanType.Pickup), TestData.Scan(ScanType.InTransit, minutesAfterNow: 5)];

        var first = await _service.UploadShipmentScansAsync(TestData.TrackingNumber, batch, Ct);
        var second = await _service.UploadShipmentScansAsync(TestData.TrackingNumber, batch, Ct);

        Assert.Equal((2, 0), (first.Accepted, first.DuplicatesSkipped));
        Assert.Equal((0, 2), (second.Accepted, second.DuplicatesSkipped));
        Assert.Equal(ShipmentStatus.InTransit, _store.Stored(TestData.TrackingNumber)!.Status);
    }

    [Fact]
    public async Task An_unknown_shipment_throws_NotFoundException_and_stores_nothing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UploadShipmentScansAsync(TestData.TrackingNumber, [TestData.Scan(ScanType.Pickup)], Ct));
        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public async Task A_scan_of_another_shipment_is_rejected_and_the_valid_scan_is_still_stored()
    {
        _store.Store(TestData.Shipment());
        _store.Store(TestData.Shipment(TestData.OtherTrackingNumber));
        var own = TestData.Scan(ScanType.Pickup);
        var foreign = TestData.Scan(ScanType.Pickup, trackingNumber: TestData.OtherTrackingNumber);

        var result = await _service.UploadShipmentScansAsync(TestData.TrackingNumber, [own, foreign], Ct);

        Assert.Equal((1, 0), (result.Accepted, result.DuplicatesSkipped));
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(foreign.ScanId, rejected.ScanId);
        Assert.Contains(TestData.TrackingNumber, rejected.Reason, StringComparison.Ordinal);
        Assert.Equal(own.ScanId, Assert.Single(_store.StoredScans).ScanId);
        Assert.Equal(ShipmentStatus.Created, _store.Stored(TestData.OtherTrackingNumber)!.Status);
    }

    [Fact]
    public async Task A_mixed_batch_counts_every_scan_exactly_once()
    {
        _store.Store(TestData.Shipment());
        _store.Store(TestData.Shipment(TestData.OtherTrackingNumber));
        var earlier = TestData.Scan(ScanType.InTransit, minutesAfterNow: 10);
        await _service.UploadShipmentScansAsync(TestData.TrackingNumber, [earlier], Ct);
        var pickup = TestData.Scan(ScanType.Pickup, minutesAfterNow: 20);
        var foreign = TestData.Scan(ScanType.InTransit, trackingNumber: TestData.OtherTrackingNumber);
        var delivered = TestData.Scan(ScanType.Delivered, minutesAfterNow: 30);
        ScanUpload[] batch = [earlier, pickup, foreign, delivered, delivered];

        var result = await _service.UploadShipmentScansAsync(TestData.TrackingNumber, batch, Ct);

        Assert.Equal((1, 2), (result.Accepted, result.DuplicatesSkipped));
        Assert.Equal([foreign.ScanId, pickup.ScanId], result.Rejected.Select(scan => scan.ScanId));
        Assert.Equal(batch.Length, result.Accepted + result.DuplicatesSkipped + result.Rejected.Count);
        Assert.Equal(ShipmentStatus.Delivered, _store.Stored(TestData.TrackingNumber)!.Status);
        Assert.Equal([earlier.ScanId, delivered.ScanId], _store.StoredScans.Select(scan => scan.ScanId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(TrackingLimits.MaximumScansPerUpload + 1)]
    public async Task An_empty_or_too_large_upload_is_a_validation_error(int count)
    {
        _store.Store(TestData.Shipment());
        var scans = Enumerable.Range(0, count).Select(_ => TestData.Scan(ScanType.Pickup)).ToList();

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _service.UploadShipmentScansAsync(TestData.TrackingNumber, scans, Ct));
    }
}
