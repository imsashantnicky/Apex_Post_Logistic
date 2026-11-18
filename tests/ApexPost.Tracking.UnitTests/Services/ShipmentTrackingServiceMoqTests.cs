using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Tracking.UnitTests.Support;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ApexPost.Tracking.UnitTests.Services;

/// <summary>
/// <see cref="ShipmentTrackingService"/> with every port replaced by a Moq mock. These tests prove the interaction
/// with the ports (what is staged, whether and how often the unit of work commits, which token is passed on); the
/// stateful <see cref="InMemoryTracking"/> fake in <see cref="ShipmentTrackingServiceTests"/> proves the outcomes.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ShipmentTrackingServiceMoqTests
{
    private const string OtherCourierId = "CR-102";

    private readonly Mock<ITrackedShipmentRepository> _shipments = new();
    private readonly Mock<ITrackingQueries> _queries = new();
    private readonly Mock<ITrackingUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentCourier> _courier = new();
    private readonly FakeTimeProvider _time = new(FixedTimeProvider.Default);
    private readonly ShipmentTrackingService _service;

    public ShipmentTrackingServiceMoqTests()
    {
        _courier.Setup(courier => courier.CourierId).Returns(TestData.CourierId);
        _shipments
            .Setup(repository => repository.FindStoredScanIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));
        _service = new ShipmentTrackingService(
            _shipments.Object, _queries.Object, _unitOfWork.Object, _courier.Object, _time, TestData.ScanAge());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateAsync_TrackingNumberInUse_ThrowsDuplicateKeyExceptionAndNeverSaves()
    {
        // Arrange
        FindReturns(TestData.Shipment());

        // Act
        var exception = await Assert.ThrowsAsync<DuplicateKeyException>(() =>
            _service.CreateAsync(TestData.TrackingNumber, TestData.Command(), Ct));

        // Assert
        Assert.Equal("Shipment APX-10000001 already exists.", exception.Message);
        _shipments.Verify(repository => repository.Add(It.IsAny<TrackedShipment>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NewTrackingNumber_AddsCreatedShipmentAndSavesOnce()
    {
        // Arrange
        FindReturns(null);
        _queries
            .Setup(queries => queries.FindAsync(TestData.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(View(ShipmentStatus.Created));

        // Act
        var view = await _service.CreateAsync(TestData.TrackingNumber, TestData.Command(), Ct);

        // Assert
        Assert.Equal(ShipmentStatus.Created, view.Status);
        _shipments.Verify(
            repository => repository.Add(It.Is<TrackedShipment>(shipment =>
                shipment.TrackingNumber == TestData.TrackingNumber
                && shipment.Status == ShipmentStatus.Created
                && shipment.CreatedAtUtc == TestData.Now)),
            Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_UnknownTrackingNumber_ThrowsNotFoundException()
    {
        // Arrange
        _queries
            .Setup(queries => queries.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShipmentView?)null);

        // Act
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetAsync(TestData.TrackingNumber, Ct));

        // Assert
        Assert.Equal("Shipment APX-10000001 does not exist.", exception.Message);
    }

    [Fact]
    public async Task ChangeStatusAsync_UnknownTrackingNumber_ThrowsNotFoundExceptionAndNeverSaves()
    {
        // Arrange
        FindReturns(null);

        // Act
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ChangeStatusAsync(TestData.TrackingNumber, ShipmentStatus.PickedUp, Ct));

        // Assert
        Assert.Equal("Shipment APX-10000001 does not exist.", exception.Message);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.Delivered, ShipmentStatus.Failed)]
    public async Task ChangeStatusAsync_InvalidTransition_ThrowsInvalidStatusTransitionExceptionAndNeverSaves(
        ShipmentStatus current,
        ShipmentStatus requested)
    {
        // Arrange
        FindReturns(ShipmentIn(current));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            _service.ChangeStatusAsync(TestData.TrackingNumber, requested, Ct));

        // Assert
        Assert.Equal(current, exception.From);
        Assert.Equal(requested, exception.To);
        Assert.Equal(
            $"Shipment APX-10000001 cannot move from {current} to {requested}; a shipment only moves forward.",
            exception.Message);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ShipmentPickedUp_ThrowsShipmentLockedExceptionAndNeverRemoves()
    {
        // Arrange
        FindReturns(ShipmentIn(ShipmentStatus.PickedUp));

        // Act
        var exception = await Assert.ThrowsAsync<ShipmentLockedException>(() =>
            _service.DeleteAsync(TestData.TrackingNumber, Ct));

        // Assert
        Assert.Equal(
            "Shipment APX-10000001 is PickedUp; only a Created shipment can be deleted.", exception.Message);
        _shipments.Verify(repository => repository.Remove(It.IsAny<TrackedShipment>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_NewScans_StagesEachScanByIdAndSavesOnce()
    {
        // Arrange
        FindReturns(TestData.Shipment());
        var pickup = TestData.Scan(ScanType.Pickup);
        var inTransit = TestData.Scan(ScanType.InTransit, minutesAfterNow: 30);

        // Act
        var result = await _service.UploadScansAsync([inTransit, pickup], Ct);

        // Assert
        Assert.Equal(2, result.Accepted);
        Assert.Equal(0, result.DuplicatesSkipped);
        Assert.Empty(result.Rejected);
        _shipments.Verify(
            repository => repository.AddScan(It.Is<TrackingScan>(scan => scan.ScanId == pickup.ScanId)), Times.Once);
        _shipments.Verify(
            repository => repository.AddScan(It.Is<TrackingScan>(scan => scan.ScanId == inTransit.ScanId)),
            Times.Once);
        _shipments.Verify(
            repository => repository.FindAsync(TestData.TrackingNumber, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadScansAsync_ScanIdsAlreadyStored_SkipsThemAsDuplicates()
    {
        // Arrange
        FindReturns(TestData.Shipment());
        var stored = TestData.Scan(ScanType.Pickup);
        var fresh = TestData.Scan(ScanType.InTransit, minutesAfterNow: 30);
        StoredScanIds(stored.ScanId);

        // Act
        var result = await _service.UploadScansAsync([stored, fresh], Ct);

        // Assert
        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);
        Assert.Empty(result.Rejected);
        _shipments.Verify(
            repository => repository.AddScan(It.Is<TrackingScan>(scan => scan.ScanId == stored.ScanId)), Times.Never);
        _shipments.Verify(
            repository => repository.AddScan(It.Is<TrackingScan>(scan => scan.ScanId == fresh.ScanId)), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadScansAsync_SameScanIdTwiceInBatch_StagesItOnceAndCountsOneDuplicate()
    {
        // Arrange
        FindReturns(TestData.Shipment());
        var scan = TestData.Scan(ScanType.Pickup);

        // Act
        var result = await _service.UploadScansAsync([scan, scan], Ct);

        // Assert
        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);
        _shipments.Verify(
            repository => repository.FindStoredScanIdsAsync(
                It.Is<IReadOnlyCollection<string>>(ids => ids.Count == 1 && ids.Contains(scan.ScanId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _shipments.Verify(repository => repository.AddScan(It.IsAny<TrackingScan>()), Times.Exactly(1));
    }

    [Fact]
    public async Task UploadScansAsync_EveryScanAlreadyStored_NeverLoadsShipmentsAndNeverSaves()
    {
        // Arrange
        var first = TestData.Scan(ScanType.Pickup);
        var second = TestData.Scan(ScanType.InTransit, minutesAfterNow: 30);
        StoredScanIds(first.ScanId, second.ScanId);

        // Act
        var result = await _service.UploadScansAsync([first, second], Ct);

        // Assert
        Assert.Equal(0, result.Accepted);
        Assert.Equal(2, result.DuplicatesSkipped);
        _shipments.Verify(
            repository => repository.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _shipments.Verify(repository => repository.AddScan(It.IsAny<TrackingScan>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_ScanOfAnotherCourier_ThrowsForbiddenExceptionAndNeverTouchesRepository()
    {
        // Arrange
        var own = TestData.Scan(ScanType.Pickup);
        var foreign = TestData.Scan(ScanType.InTransit, courierId: OtherCourierId);

        // Act
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.UploadScansAsync([own, foreign], Ct));

        // Assert
        Assert.Equal(
            $"Courier CR-101 cannot upload scan {foreign.ScanId}, which was recorded by CR-102.", exception.Message);
        _shipments.VerifyNoOtherCalls();
        _queries.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CR-1")]
    public async Task UploadScansAsync_CallerIsNotACourier_ThrowsForbiddenExceptionAndNeverTouchesRepository(
        string? callerId)
    {
        // Arrange
        _courier.Setup(courier => courier.CourierId).Returns(callerId);

        // Act
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.UploadScansAsync([TestData.Scan(ScanType.Pickup)], Ct));

        // Assert
        Assert.Equal("Only a signed-in courier can upload scans.", exception.Message);
        _shipments.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadScansAsync_CallerToken_IsPassedToEveryRepositoryAndUnitOfWorkCall()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var token = source.Token;
        FindReturns(TestData.Shipment());

        // Act
        await _service.UploadScansAsync([TestData.Scan(ScanType.Pickup)], token);

        // Assert
        _shipments.Verify(
            repository => repository.FindStoredScanIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), token),
            Times.Once);
        _shipments.Verify(repository => repository.FindAsync(TestData.TrackingNumber, token), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(token), Times.Once);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.Is<CancellationToken>(ct => ct != token)), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_CallerToken_IsPassedToRepositoryUnitOfWorkAndQueries()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var token = source.Token;
        FindReturns(null);
        _queries
            .Setup(queries => queries.FindAsync(TestData.TrackingNumber, token))
            .ReturnsAsync(View(ShipmentStatus.Created));

        // Act
        await _service.CreateAsync(TestData.TrackingNumber, TestData.Command(), token);

        // Assert
        _shipments.Verify(repository => repository.FindAsync(TestData.TrackingNumber, token), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(token), Times.Once);
        _queries.Verify(queries => queries.FindAsync(TestData.TrackingNumber, token), Times.Once);
    }

    [Fact]
    public async Task UploadScansAsync_SaveHitsDuplicateKey_DiscardsChangesAndCountsStoredScanAsDuplicate()
    {
        // Arrange
        FindReturns(TestData.Shipment());
        var scan = TestData.Scan(ScanType.Pickup);
        _shipments
            .SetupSequence(repository => repository.FindStoredScanIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal))
            .ReturnsAsync(new HashSet<string>([scan.ScanId], StringComparer.Ordinal));
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateKeyException("A parallel upload stored the scan first."));

        // Act
        var result = await _service.UploadScansAsync([scan], Ct);

        // Assert
        Assert.Equal(0, result.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);
        _unitOfWork.Verify(unitOfWork => unitOfWork.DiscardChanges(), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private void FindReturns(TrackedShipment? shipment) =>
        _shipments
            .Setup(repository => repository.FindAsync(TestData.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

    private void StoredScanIds(params string[] scanIds) =>
        _shipments
            .Setup(repository => repository.FindStoredScanIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(scanIds, StringComparer.Ordinal));

    private static TrackedShipment ShipmentIn(ShipmentStatus status)
    {
        var shipment = TestData.Shipment();
        if (status != ShipmentStatus.Created)
        {
            shipment.MoveTo(status);
        }

        return shipment;
    }

    private static ShipmentView View(ShipmentStatus status) =>
        new(
            TestData.TrackingNumber,
            "Mia Example",
            "+44 20 7946 0001",
            "1 Harbour Road",
            "Harbour Point",
            2.5m,
            status,
            TestData.Now,
            TrackedShipment.FirstVersion,
            null);
}
