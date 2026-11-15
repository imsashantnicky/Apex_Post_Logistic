using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ApexPost.Courier.UnitTests.Offline;

/// <summary>
/// The single-scan path of <see cref="OutboxService"/> with its three ports mocked: which scans are sent, which
/// status each one gets and how often the uploader is called. The same service on a real embedded Couchbase
/// database is tested in <c>ApexPost.Courier.Offline.Tests/Outbox/OutboxServiceTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutboxServiceMoqTests : IDisposable
{
    private const string TrackingNumber = "APX-10000001";
    private const string CourierId = "CR-101";
    private static readonly DateTimeOffset FirstScanAt = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<IScanQueries> _queries = new();
    private readonly Mock<IScanSyncStatusUpdater> _statusUpdater = new();
    private readonly Mock<IShipmentUploader> _uploader = new();
    private readonly OutboxService _outbox;

    public OutboxServiceMoqTests()
    {
        _statusUpdater
            .Setup(updater => updater.UpdateSyncStatusAsync(
                It.IsAny<string>(), It.IsAny<SyncStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _queries.Setup(queries => queries.GetFailedAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _outbox = new OutboxService(
            _queries.Object, _statusUpdater.Object, _uploader.Object, NullLogger<OutboxService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _outbox.Dispose();

    [Fact]
    public async Task ProcessPendingAsync_UploadSucceeds_MarksThatScanSynced()
    {
        // Arrange
        var scan = PendingScan(0);
        PendingAre(scan);
        _uploader.Setup(uploader => uploader.UploadAsync(scan, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult(true, null));

        // Act
        var result = await _outbox.ProcessPendingAsync(ct: Ct);

        // Assert
        Assert.Equal(new OutboxResult(1, 1, 0), result);
        VerifyStatus(scan, SyncStatus.Synced, Times.Once());
        VerifyStatus(scan, SyncStatus.Failed, Times.Never());
    }

    [Fact]
    public async Task ProcessPendingAsync_UploadReturnsFailure_MarksThatScanFailed()
    {
        // Arrange
        var scan = PendingScan(0);
        PendingAre(scan);
        _uploader.Setup(uploader => uploader.UploadAsync(scan, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult(false, "Tracking number is unknown."));

        // Act
        var result = await _outbox.ProcessPendingAsync(ct: Ct);

        // Assert
        Assert.Equal(new OutboxResult(1, 0, 1), result);
        VerifyStatus(scan, SyncStatus.Failed, Times.Once());
        VerifyStatus(scan, SyncStatus.Synced, Times.Never());
    }

    [Fact]
    public async Task ProcessPendingAsync_UploaderThrowsForOneScan_UploadsTheRestAndMarksOnlyThatOneFailed()
    {
        // Arrange
        var first = PendingScan(0);
        var broken = PendingScan(1);
        var last = PendingScan(2);
        PendingAre(first, broken, last);
        _uploader.Setup(uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult(true, null));
        _uploader.Setup(uploader => uploader.UploadAsync(
                It.Is<ShipmentScan>(scan => scan.Id == broken.Id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection reset."));

        // Act
        var result = await _outbox.ProcessPendingAsync(ct: Ct);

        // Assert
        Assert.Equal(new OutboxResult(3, 2, 1), result);
        _uploader.Verify(
            uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        VerifyStatus(first, SyncStatus.Synced, Times.Once());
        VerifyStatus(broken, SyncStatus.Failed, Times.Once());
        VerifyStatus(last, SyncStatus.Synced, Times.Once());
    }

    [Fact]
    public async Task ProcessPendingAsync_NothingPending_NeverCallsUploaderOrStatusUpdater()
    {
        // Arrange
        PendingAre();

        // Act
        var result = await _outbox.ProcessPendingAsync(ct: Ct);

        // Assert
        Assert.Equal(new OutboxResult(0, 0, 0), result);
        _uploader.Verify(
            uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Never);
        _statusUpdater.Verify(
            updater => updater.UpdateSyncStatusAsync(
                It.IsAny<string>(), It.IsAny<SyncStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _queries.Verify(queries => queries.GetFailedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPendingAsync_IncludeFailed_UploadsPendingAndFailedScansOldestFirst()
    {
        // Arrange
        var olderFailed = PendingScan(0);
        var newerPending = PendingScan(5);
        PendingAre(newerPending);
        _queries.Setup(queries => queries.GetFailedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([olderFailed]);
        var sent = new List<string>();
        _uploader.Setup(uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .Callback<ShipmentScan, CancellationToken>((scan, _) => sent.Add(scan.Id))
            .ReturnsAsync(new UploadResult(true, null));

        // Act
        var result = await _outbox.ProcessPendingAsync(includeFailed: true, ct: Ct);

        // Assert
        Assert.Equal(new OutboxResult(2, 2, 0), result);
        Assert.Equal([olderFailed.Id, newerPending.Id], sent);
    }

    [Fact]
    public async Task ProcessPendingAsync_CallerToken_IsPassedToQueriesAndUploaderButNotToStatusSave()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var token = source.Token;
        var scan = PendingScan(0);
        _queries.Setup(queries => queries.GetPendingAsync(token)).ReturnsAsync([scan]);
        _uploader.Setup(uploader => uploader.UploadAsync(scan, token)).ReturnsAsync(new UploadResult(true, null));

        // Act
        await _outbox.ProcessPendingAsync(ct: token);

        // Assert
        _queries.Verify(queries => queries.GetPendingAsync(token), Times.Once);
        _uploader.Verify(uploader => uploader.UploadAsync(scan, token), Times.Once);
        _statusUpdater.Verify(
            updater => updater.UpdateSyncStatusAsync(scan.Id, SyncStatus.Synced, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingAsync_TokenAlreadyCancelled_ThrowsAndNeverCallsUploader()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        PendingAre(PendingScan(0));

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _outbox.ProcessPendingAsync(ct: source.Token));

        // Assert
        _uploader.Verify(
            uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ShipmentScan PendingScan(int minutesAfterFirst) =>
        ShipmentScan.Restore(
            ScanIds.New(),
            TrackingNumber,
            ScanType.InTransit,
            CourierId,
            "Central Hub",
            FirstScanAt.AddMinutes(minutesAfterFirst),
            null,
            SyncStatus.Pending);

    private void PendingAre(params ShipmentScan[] scans) =>
        _queries.Setup(queries => queries.GetPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(scans);

    private void VerifyStatus(ShipmentScan scan, SyncStatus status, Times times) =>
        _statusUpdater.Verify(
            updater => updater.UpdateSyncStatusAsync(scan.Id, status, It.IsAny<CancellationToken>()), times);
}
