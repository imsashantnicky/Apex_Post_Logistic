using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Common;
using ApexPost.Domain.Tracking;
using ApexPost.Tracking.UnitTests.Support;

namespace ApexPost.Tracking.UnitTests.Services;

public sealed class ShipmentTrackingServiceTests
{
    private readonly InMemoryTracking _store = new();
    private readonly ShipmentTrackingService _service;

    public ShipmentTrackingServiceTests()
    {
        _service = new ShipmentTrackingService(_store, _store, _store, new FixedCourier(), new FixedTimeProvider());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_stores_a_Created_shipment_with_one_commit()
    {
        var view = await _service.CreateAsync(TestData.TrackingNumber, TestData.Command(), Ct);

        Assert.Equal(ShipmentStatus.Created, view.Status);
        Assert.Equal(TestData.Now, view.CreatedAtUtc);
        Assert.Null(view.LatestScan);
        Assert.Equal(1, _store.Saves);
    }

    [Fact]
    public async Task Create_with_a_tracking_number_in_use_throws_DuplicateKeyException()
    {
        _store.Store(TestData.Shipment());

        await Assert.ThrowsAsync<DuplicateKeyException>(() =>
            _service.CreateAsync(TestData.TrackingNumber, TestData.Command(), Ct));
        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public async Task Create_with_invalid_input_throws_DomainException_and_stores_nothing()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync("APX-1", TestData.Command(), Ct));
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(TestData.TrackingNumber, TestData.Command(weightKg: 31m), Ct));
        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public async Task Get_and_GetScans_of_an_unknown_tracking_number_throw_NotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(TestData.TrackingNumber, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetScansAsync(TestData.TrackingNumber, Ct));
    }

    [Fact]
    public async Task Replace_changes_recipient_and_weight()
    {
        _store.Store(TestData.Shipment());

        var view = await _service.ReplaceAsync(TestData.TrackingNumber, TestData.Command(4m, "Northgate"), Ct);

        Assert.Equal(4m, view.WeightKg);
        Assert.Equal("Northgate", view.RecipientCity);
    }

    [Fact]
    public async Task Replace_of_a_final_shipment_throws_ShipmentLockedException()
    {
        var shipment = TestData.Shipment();
        shipment.MoveTo(ShipmentStatus.Failed);
        _store.Store(shipment);

        await Assert.ThrowsAsync<ShipmentLockedException>(() =>
            _service.ReplaceAsync(TestData.TrackingNumber, TestData.Command(), Ct));
    }

    [Fact]
    public async Task ChangeStatus_moves_forward()
    {
        _store.Store(TestData.Shipment());

        var view = await _service.ChangeStatusAsync(TestData.TrackingNumber, ShipmentStatus.PickedUp, Ct);

        Assert.Equal(ShipmentStatus.PickedUp, view.Status);
        Assert.Equal(1, _store.Saves);
    }

    [Theory]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.Delivered, ShipmentStatus.Failed)]
    public async Task ChangeStatus_backward_same_or_after_final_throws_InvalidStatusTransitionException(
        ShipmentStatus current,
        ShipmentStatus wanted)
    {
        var shipment = TestData.Shipment();
        shipment.MoveTo(current);
        _store.Store(shipment);

        var error = await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            _service.ChangeStatusAsync(TestData.TrackingNumber, wanted, Ct));

        Assert.Equal(current, error.From);
        Assert.Equal(wanted, error.To);
        Assert.Equal(current, shipment.Status);
        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public async Task ChangeStatus_to_an_undefined_status_is_a_validation_error()
    {
        _store.Store(TestData.Shipment());

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _service.ChangeStatusAsync(TestData.TrackingNumber, (ShipmentStatus)99, Ct));
    }

    [Fact]
    public async Task Delete_removes_a_Created_shipment()
    {
        _store.Store(TestData.Shipment());

        await _service.DeleteAsync(TestData.TrackingNumber, Ct);

        Assert.Null(_store.Stored(TestData.TrackingNumber));
    }

    [Fact]
    public async Task Delete_after_Created_throws_ShipmentLockedException()
    {
        var shipment = TestData.Shipment();
        shipment.MoveTo(ShipmentStatus.PickedUp);
        _store.Store(shipment);

        await Assert.ThrowsAsync<ShipmentLockedException>(() => _service.DeleteAsync(TestData.TrackingNumber, Ct));
        Assert.NotNull(_store.Stored(TestData.TrackingNumber));
    }

    [Fact]
    public async Task Delete_of_an_unknown_shipment_throws_NotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(TestData.TrackingNumber, Ct));
    }

    [Fact]
    public async Task List_filters_by_status_and_city_and_pages()
    {
        for (var number = 1; number <= 3; number++)
        {
            _store.Store(TestData.Shipment($"APX-1000000{number}"));
        }

        _store.Stored("APX-10000003")!.MoveTo(ShipmentStatus.PickedUp);

        var created = await _service.ListAsync(1, 1, ShipmentStatus.Created, " harbour point ", Ct);

        Assert.Equal(2, created.TotalCount);
        Assert.Equal("APX-10000001", Assert.Single(created.Items).TrackingNumber);
        Assert.True(created.HasNext);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task List_rejects_a_bad_page_or_page_size(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _service.ListAsync(page, pageSize, null, null, Ct));
    }

    [Fact]
    public async Task List_accepts_the_largest_page_size()
    {
        var page = await _service.ListAsync(1, TrackingLimits.MaximumPageSize, null, null, Ct);

        Assert.Equal(TrackingLimits.MaximumPageSize, page.PageSize);
    }

    [Theory]
    [InlineData(ScanType.Pickup, ShipmentStatus.PickedUp)]
    [InlineData(ScanType.InTransit, ShipmentStatus.InTransit)]
    [InlineData(ScanType.Delivered, ShipmentStatus.Delivered)]
    [InlineData(ScanType.Failed, ShipmentStatus.Failed)]
    public async Task An_uploaded_scan_moves_a_Created_shipment_to_its_status(ScanType type, ShipmentStatus expected)
    {
        _store.Store(TestData.Shipment());

        var result = await _service.UploadScansAsync([TestData.Scan(type)], Ct);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(expected, _store.Stored(TestData.TrackingNumber)!.Status);
    }

    [Fact]
    public async Task Scans_in_one_batch_are_applied_oldest_first()
    {
        _store.Store(TestData.Shipment());
        ScanUpload[] batch =
        [
            TestData.Scan(ScanType.Delivered, minutesAfterNow: 30),
            TestData.Scan(ScanType.Pickup, minutesAfterNow: 0),
            TestData.Scan(ScanType.InTransit, minutesAfterNow: 10),
        ];

        var result = await _service.UploadScansAsync(batch, Ct);
        var view = await _service.GetAsync(TestData.TrackingNumber, Ct);

        Assert.Equal(3, result.Accepted);
        Assert.Equal(ShipmentStatus.Delivered, view.Status);
        Assert.Equal(ScanType.Delivered, view.LatestScan?.ScanType);
        Assert.Equal(1, _store.Saves);
    }

    [Fact]
    public async Task A_backward_scan_is_rejected_and_the_status_does_not_move()
    {
        var shipment = TestData.Shipment();
        shipment.MoveTo(ShipmentStatus.InTransit);
        _store.Store(shipment);
        var pickup = TestData.Scan(ScanType.Pickup);

        var result = await _service.UploadScansAsync([pickup], Ct);

        Assert.Equal((0, 0), (result.Accepted, result.DuplicatesSkipped));
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(pickup.ScanId, rejected.ScanId);
        Assert.Contains("from InTransit to PickedUp", rejected.Reason, StringComparison.Ordinal);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
        Assert.Empty(_store.StoredScans);
        Assert.Equal(0, _store.Saves);
    }

    [Theory]
    [InlineData(ShipmentStatus.Delivered, ScanType.Delivered)]
    [InlineData(ShipmentStatus.Delivered, ScanType.Failed)]
    [InlineData(ShipmentStatus.Failed, ScanType.Delivered)]
    [InlineData(ShipmentStatus.Failed, ScanType.InTransit)]
    public async Task No_scan_is_accepted_after_Delivered_or_Failed(ShipmentStatus final, ScanType type)
    {
        var shipment = TestData.Shipment();
        shipment.MoveTo(final);
        _store.Store(shipment);

        var result = await _service.UploadScansAsync([TestData.Scan(type)], Ct);

        Assert.Equal(0, result.Accepted);
        Assert.Single(result.Rejected);
        Assert.Equal(final, shipment.Status);
    }

    [Fact]
    public async Task A_second_InTransit_scan_is_stored_and_keeps_the_status()
    {
        var shipment = TestData.Shipment();
        shipment.MoveTo(ShipmentStatus.InTransit);
        _store.Store(shipment);

        var result = await _service.UploadScansAsync([TestData.Scan(ScanType.InTransit)], Ct);

        Assert.Equal(1, result.Accepted);
        Assert.Empty(result.Rejected);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
        Assert.Single(_store.StoredScans);
    }

    [Fact]
    public async Task A_scan_after_a_final_scan_in_the_same_batch_is_rejected()
    {
        _store.Store(TestData.Shipment());
        var failed = TestData.Scan(ScanType.Failed, minutesAfterNow: 10);
        var delivered = TestData.Scan(ScanType.Delivered, minutesAfterNow: 20);

        var result = await _service.UploadScansAsync([delivered, failed], Ct);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(delivered.ScanId, Assert.Single(result.Rejected).ScanId);
        Assert.Equal(ShipmentStatus.Failed, _store.Stored(TestData.TrackingNumber)!.Status);
    }

    [Fact]
    public async Task Uploading_the_same_batch_twice_skips_every_scan_the_second_time()
    {
        _store.Store(TestData.Shipment());
        ScanUpload[] batch = [TestData.Scan(ScanType.Pickup), TestData.Scan(ScanType.InTransit, minutesAfterNow: 5)];

        var first = await _service.UploadScansAsync(batch, Ct);
        var second = await _service.UploadScansAsync(batch, Ct);

        Assert.Equal((2, 0), (first.Accepted, first.DuplicatesSkipped));
        Assert.Equal((0, 2), (second.Accepted, second.DuplicatesSkipped));
        Assert.Equal(2, _store.StoredScans.Count);
        Assert.Equal(1, _store.Saves);
    }

    [Fact]
    public async Task A_scan_id_repeated_inside_one_batch_is_a_duplicate()
    {
        _store.Store(TestData.Shipment());
        var scan = TestData.Scan(ScanType.Pickup);

        var result = await _service.UploadScansAsync([scan, scan with { Location = "Elsewhere" }], Ct);

        Assert.Equal((1, 1), (result.Accepted, result.DuplicatesSkipped));
        Assert.Equal("Central Hub", Assert.Single(_store.StoredScans).Location);
    }

    [Fact]
    public async Task A_rejected_scan_repeated_inside_one_batch_is_rejected_once()
    {
        var scan = TestData.Scan(ScanType.Pickup);

        var result = await _service.UploadScansAsync([scan, scan, scan], Ct);

        Assert.Equal((0, 2), (result.Accepted, result.DuplicatesSkipped));
        Assert.Equal(scan.ScanId, Assert.Single(result.Rejected).ScanId);
    }

    [Fact]
    public async Task Bad_scans_are_rejected_with_a_reason_and_the_rest_are_stored()
    {
        _store.Store(TestData.Shipment());
        var unknown = TestData.Scan(ScanType.Pickup, trackingNumber: TestData.OtherTrackingNumber);
        var malformed = TestData.Scan(ScanType.Pickup, trackingNumber: "APX-1");
        var badCourier = TestData.Scan(ScanType.Pickup, courierId: "CR-1");
        var good = TestData.Scan(ScanType.Pickup);

        var result = await _service.UploadScansAsync([unknown, malformed, badCourier, good], Ct);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(
            [unknown.ScanId, malformed.ScanId, badCourier.ScanId],
            result.Rejected.Select(rejected => rejected.ScanId));
        Assert.All(result.Rejected, rejected => Assert.False(string.IsNullOrWhiteSpace(rejected.Reason)));
        Assert.Equal(good.ScanId, Assert.Single(_store.StoredScans).ScanId);
    }

    [Fact]
    public async Task An_upload_with_only_rejected_scans_commits_nothing()
    {
        var result = await _service.UploadScansAsync([TestData.Scan(ScanType.Pickup)], Ct);

        Assert.Equal(0, result.Accepted);
        Assert.Single(result.Rejected);
        Assert.Equal(0, _store.Saves);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(TrackingLimits.MaximumScansPerUpload + 1)]
    public async Task An_empty_or_too_large_upload_is_a_validation_error(int count)
    {
        var scans = Enumerable.Range(0, count).Select(_ => TestData.Scan(ScanType.Pickup)).ToList();

        await Assert.ThrowsAsync<ValidationFailedException>(() => _service.UploadScansAsync(scans, Ct));
    }

    [Fact]
    public async Task When_a_parallel_upload_stores_the_scan_first_the_retry_counts_it_as_a_duplicate()
    {
        var shipment = TestData.Shipment();
        _store.Store(shipment);
        var scan = TestData.Scan(ScanType.Pickup);
        _store.BeforeNextSave = () =>
        {
            _store.StoreScan(TrackingScan.Record(shipment, scan.Details, TestData.Now));
            throw new DuplicateKeyException("The other upload won.");
        };

        var result = await _service.UploadScansAsync([scan], Ct);

        Assert.Equal((0, 1), (result.Accepted, result.DuplicatesSkipped));
        Assert.Equal(1, _store.Discards);
        Assert.Single(_store.StoredScans);
    }

    [Fact]
    public async Task A_second_conflict_in_a_row_is_not_hidden()
    {
        _store.Store(TestData.Shipment());
        _store.BeforeNextSave = () =>
        {
            _store.BeforeNextSave = () => throw new ConcurrencyConflictException("Again.");
            throw new ConcurrencyConflictException("First.");
        };

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            _service.UploadScansAsync([TestData.Scan(ScanType.Pickup)], Ct));
    }
}
