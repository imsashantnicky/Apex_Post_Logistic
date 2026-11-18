// Copilot-assisted: drafted by GitHub Copilot, reviewed and edited by a human (see COPILOT-LOG.md).
using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ApexPost.Tracking.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class ShipmentTrackingServiceCopilotTests
{
    private const string TrackingNumber = "APX-10000001";
    private const string UnknownTrackingNumber = "APX-10000009";
    private const string CourierId = "CR-101";
    private const string ScanAgeRejection = "Scan is older than 72 hours.";

    private readonly Mock<ITrackedShipmentRepository> _shipments = new();
    private readonly Mock<ITrackingQueries> _queries = new();
    private readonly Mock<ITrackingUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentCourier> _courier = new();
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero));
    private readonly ShipmentTrackingService _service;

    public ShipmentTrackingServiceCopilotTests()
    {
        _courier.Setup(courier => courier.CourierId).Returns(CourierId);
        _shipments
            .Setup(repository => repository.FindStoredScanIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));

        var scanAge = new ScanAgePolicy(
            Options.Create(new TrackingOptions { MaxScanAgeHours = 72 }));

        _service = new ShipmentTrackingService(
            _shipments.Object,
            _queries.Object,
            _unitOfWork.Object,
            _courier.Object,
            _time,
            scanAge);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateTime NowUtc => _time.GetUtcNow().UtcDateTime;

    [Fact]
    public async Task UploadScansAsync_ValidPickupScan_AcceptsAndStagesScan()
    {
        // Arrange
        var shipment = NewShipment();
        var scan = NewScan(ScanType.Pickup);
        FindShipment(shipment);

        // Act
        var result = await _service.UploadScansAsync([scan], Ct);

        // Assert
        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, result.DuplicatesSkipped);
        Assert.Empty(result.Rejected);
        Assert.Equal(ShipmentStatus.PickedUp, shipment.Status);
        _shipments.Verify(
            repository => repository.AddScan(It.Is<TrackingScan>(stored =>
                stored.ScanId == scan.ScanId
                && stored.ScanType == ScanType.Pickup
                && stored.CourierId == CourierId)),
            Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(Ct), Times.Once);
    }

    [Fact]
    public async Task UploadScansAsync_UnknownTrackingNumber_RejectsWithoutSaving()
    {
        // Arrange
        var scan = NewScan(ScanType.Pickup, UnknownTrackingNumber);
        FindShipment(null, UnknownTrackingNumber);

        // Act
        var result = await _service.UploadScansAsync([scan], Ct);

        // Assert
        Assert.Equal(0, result.Accepted);
        Assert.Equal(0, result.DuplicatesSkipped);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(scan.ScanId, rejected.ScanId);
        Assert.Equal("Unknown tracking number.", rejected.Reason);
        _shipments.Verify(repository => repository.AddScan(It.IsAny<TrackingScan>()), Times.Never);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(71, 0, true)]
    [InlineData(72, 0, true)]
    [InlineData(72, 1, false)]
    [InlineData(73, 0, false)]
    public async Task UploadScansAsync_ScanAgeBoundary_AcceptsWithinLimitAndRejectsOlder(
        int hoursOld,
        int extraSecondsOld,
        bool accepted)
    {
        // Arrange
        var scan = NewScan(
            ScanType.Pickup,
            scannedAtUtc: NowUtc.AddHours(-hoursOld).AddSeconds(-extraSecondsOld));
        FindShipment(NewShipment());

        // Act
        var result = await _service.UploadScansAsync([scan], Ct);

        // Assert
        if (accepted)
        {
            Assert.Equal(1, result.Accepted);
            Assert.Empty(result.Rejected);
            _shipments.Verify(
                repository => repository.AddScan(It.Is<TrackingScan>(stored => stored.ScanId == scan.ScanId)),
                Times.Once);
            _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(Ct), Times.Once);
        }
        else
        {
            Assert.Equal(0, result.Accepted);
            var rejected = Assert.Single(result.Rejected);
            Assert.Equal(scan.ScanId, rejected.ScanId);
            Assert.Equal(ScanAgeRejection, rejected.Reason);
            _shipments.Verify(repository => repository.AddScan(It.IsAny<TrackingScan>()), Times.Never);
            _unitOfWork.Verify(
                unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    [Theory]
    [InlineData(
        ShipmentStatus.InTransit,
        ScanType.Pickup,
        "A Pickup scan cannot move shipment APX-10000001 from InTransit to PickedUp.")]
    [InlineData(
        ShipmentStatus.Delivered,
        ScanType.Pickup,
        "A Pickup scan cannot move shipment APX-10000001 from Delivered to PickedUp.")]
    [InlineData(
        ShipmentStatus.Delivered,
        ScanType.InTransit,
        "A InTransit scan cannot move shipment APX-10000001 from Delivered to InTransit.")]
    [InlineData(
        ShipmentStatus.Delivered,
        ScanType.Delivered,
        "A Delivered scan cannot move shipment APX-10000001 from Delivered to Delivered.")]
    [InlineData(
        ShipmentStatus.Delivered,
        ScanType.Failed,
        "A Failed scan cannot move shipment APX-10000001 from Delivered to Failed.")]
    public async Task UploadScansAsync_BackwardOrAfterDelivered_RejectsWithReason(
        ShipmentStatus currentStatus,
        ScanType scanType,
        string expectedReason)
    {
        // Arrange
        var scan = NewScan(scanType);
        FindShipment(NewShipment(currentStatus));

        // Act
        var result = await _service.UploadScansAsync([scan], Ct);

        // Assert
        Assert.Equal(0, result.Accepted);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(scan.ScanId, rejected.ScanId);
        Assert.Equal(expectedReason, rejected.Reason);
        _shipments.Verify(repository => repository.AddScan(It.IsAny<TrackingScan>()), Times.Never);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task UploadScansAsync_InvalidBatchSize_ThrowsValidationFailedException(int count)
    {
        // Arrange
        var scans = Enumerable.Range(0, count)
            .Select(_ => NewScan(ScanType.Pickup))
            .ToArray();

        // Act
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _service.UploadScansAsync(scans, Ct));

        // Assert
        Assert.Equal("One or more fields are not valid.", exception.Message);
        var error = Assert.Single(exception.Errors);
        Assert.Equal("scans", error.Key);
        Assert.Equal(
            ["An upload must contain between 1 and 100 scans."],
            error.Value);
        _shipments.Verify(repository => repository.FindStoredScanIdsAsync(
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_SaveThrowsDuplicateKey_DiscardsAndRetriesUpload()
    {
        // Arrange
        var scan = NewScan(ScanType.Pickup);
        var firstShipment = NewShipment();
        var retryShipment = NewShipment();

        _shipments
            .SetupSequence(repository => repository.FindAsync(
                TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstShipment)
            .ReturnsAsync(retryShipment);

        _shipments
            .SetupSequence(repository => repository.FindStoredScanIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));

        _unitOfWork
            .SetupSequence(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateKeyException("A scan with this id already exists."))
            .Returns(Task.CompletedTask);

        var stagedScanCount = 0;
        _shipments
            .Setup(repository => repository.AddScan(It.IsAny<TrackingScan>()))
            .Callback<TrackingScan>(_ => stagedScanCount++);

        // Act
        var result = await _service.UploadScansAsync([scan], Ct);

        // Assert
        Assert.Equal(1, result.Accepted);
        Assert.Empty(result.Rejected);
        Assert.Equal(2, stagedScanCount);
        Assert.Equal(ShipmentStatus.PickedUp, retryShipment.Status);
        _unitOfWork.Verify(unitOfWork => unitOfWork.DiscardChanges(), Times.Once);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Theory]
    [InlineData(ShipmentStatus.Created, ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.Created, ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.Created, ShipmentStatus.OutForDelivery)]
    [InlineData(ShipmentStatus.Created, ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Created, ShipmentStatus.Failed)]
    [InlineData(ShipmentStatus.PickedUp, ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.PickedUp, ShipmentStatus.OutForDelivery)]
    [InlineData(ShipmentStatus.PickedUp, ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.PickedUp, ShipmentStatus.Failed)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.OutForDelivery)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.Failed)]
    [InlineData(ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.OutForDelivery, ShipmentStatus.Failed)]
    public async Task ChangeStatusAsync_AllowedForwardMove_UpdatesAndReturnsQueryView(
        ShipmentStatus currentStatus,
        ShipmentStatus requestedStatus)
    {
        // Arrange
        var shipment = NewShipment(currentStatus);
        var queryView = NewView(requestedStatus);
        FindShipment(shipment);
        _queries
            .Setup(queries => queries.FindAsync(TrackingNumber, Ct))
            .ReturnsAsync(queryView);

        // Act
        var result = await _service.ChangeStatusAsync(TrackingNumber, requestedStatus, Ct);

        // Assert
        Assert.Equal(requestedStatus, shipment.Status);
        Assert.Same(queryView, result);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(Ct), Times.Once);
        _queries.Verify(queries => queries.FindAsync(TrackingNumber, Ct), Times.Once);
    }

    [Fact]
    public async Task ChangeStatusAsync_UndefinedStatus_ThrowsValidationFailedException()
    {
        // Arrange
        const ShipmentStatus undefinedStatus = (ShipmentStatus)99;

        // Act
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _service.ChangeStatusAsync(TrackingNumber, undefinedStatus, Ct));

        // Assert
        Assert.Equal("One or more fields are not valid.", exception.Message);
        var error = Assert.Single(exception.Errors);
        Assert.Equal("status", error.Key);
        Assert.Equal(["Status '99' is not valid."], error.Value);
        _shipments.Verify(
            repository => repository.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeStatusAsync_CallerToken_IsPassedToRepositoryAndUnitOfWork()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        FindShipment(NewShipment());
        _queries
            .Setup(queries => queries.FindAsync(TrackingNumber, token))
            .ReturnsAsync(NewView(ShipmentStatus.PickedUp));

        // Act
        await _service.ChangeStatusAsync(TrackingNumber, ShipmentStatus.PickedUp, token);

        // Assert
        _shipments.Verify(repository => repository.FindAsync(TrackingNumber, token), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(token), Times.Once);
    }

    private void FindShipment(TrackedShipment? shipment, string trackingNumber = TrackingNumber) =>
        _shipments
            .Setup(repository => repository.FindAsync(trackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

    private TrackedShipment NewShipment(ShipmentStatus status = ShipmentStatus.Created)
    {
        var shipment = TrackedShipment.Create(
            TrackingNumber,
            new Recipient("Mia Example", "+44 20 7946 0001", "1 Harbour Road", "Harbour Point"),
            2.5m,
            NowUtc);

        if (status == ShipmentStatus.Failed)
        {
            shipment.MoveTo(ShipmentStatus.Failed);
            return shipment;
        }

        for (var next = (int)ShipmentStatus.PickedUp; next <= (int)status; next++)
        {
            shipment.MoveTo((ShipmentStatus)next);
        }

        return shipment;
    }

    private ScanUpload NewScan(
        ScanType scanType,
        string trackingNumber = TrackingNumber,
        DateTime? scannedAtUtc = null,
        string courierId = CourierId) =>
        new(
            $"scan::{Guid.NewGuid():D}",
            trackingNumber,
            scanType,
            courierId,
            "Central Hub",
            scannedAtUtc ?? NowUtc,
            null);

    private ShipmentView NewView(ShipmentStatus status) =>
        new(
            TrackingNumber,
            "Mia Example",
            "+44 20 7946 0001",
            "1 Harbour Road",
            "Harbour Point",
            2.5m,
            status,
            NowUtc,
            TrackedShipment.FirstVersion,
            null);
}
