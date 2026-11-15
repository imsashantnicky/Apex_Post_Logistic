using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.UnitTests.Offline;

public sealed class OutboxServiceTests : IDisposable
{
    private static readonly DateTimeOffset BaseTime = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "apexpost-courier-tests", Guid.NewGuid().ToString("N"));

    private readonly CourierDatabase _database;
    private readonly CouchbaseScanRepository _repository;

    public OutboxServiceTests()
    {
        _database = new CourierDatabase(_directory);
        _repository = new CouchbaseScanRepository(_database);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OutboxService CreateOutbox(IShipmentUploader uploader) =>
        new(_repository, _repository, uploader, NullLogger<OutboxService>.Instance);

    private async Task<ShipmentScan> AddAsync(
        string trackingNumber = "APX-10000001",
        int minutes = 0,
        SyncStatus status = SyncStatus.Pending)
    {
        var scan = ShipmentScan.Restore(
            ScanIds.New(), trackingNumber, ScanType.Pickup, "CR-101", "BR001", BaseTime.AddMinutes(minutes), null,
            status);
        await _repository.CreateAsync(scan, Ct);
        return scan;
    }

    private async Task<SyncStatus> StatusOfAsync(ShipmentScan scan) =>
        (await _repository.GetByIdAsync(scan.Id, Ct))!.SyncStatus;

    [Fact]
    public async Task Accepted_pending_scans_become_synced_oldest_first()
    {
        var later = await AddAsync(minutes: 10);
        var earlier = await AddAsync(minutes: 0);
        var uploader = StubShipmentUploader.AcceptAll();
        using var outbox = CreateOutbox(uploader);

        var result = await outbox.ProcessPendingAsync(ct: Ct);

        Assert.Equal(new OutboxResult(2, 2, 0), result);
        Assert.Equal([earlier.Id, later.Id], uploader.UploadedIds);
        Assert.Equal(SyncStatus.Synced, await StatusOfAsync(earlier));
        Assert.Equal(SyncStatus.Synced, await StatusOfAsync(later));
    }

    [Fact]
    public async Task A_rejected_scan_becomes_failed_and_the_rest_still_sync()
    {
        var bad = await AddAsync(trackingNumber: FakeShipmentUploader.DemoFailureTrackingNumber, minutes: 0);
        var good = await AddAsync(minutes: 5);
        using var outbox = CreateOutbox(new FakeShipmentUploader());

        var result = await outbox.ProcessPendingAsync(ct: Ct);

        Assert.Equal(new OutboxResult(2, 1, 1), result);
        Assert.Equal(SyncStatus.Failed, await StatusOfAsync(bad));
        Assert.Equal(SyncStatus.Synced, await StatusOfAsync(good));
    }

    [Fact]
    public async Task An_uploader_exception_marks_only_that_scan_failed()
    {
        var throwing = await AddAsync(minutes: 0);
        var good = await AddAsync(minutes: 5);
        var uploader = new StubShipmentUploader((scan, _) => scan.Id == throwing.Id
            ? throw new HttpRequestException("Server unreachable")
            : Task.FromResult(StubShipmentUploader.Accepted));
        using var outbox = CreateOutbox(uploader);

        var result = await outbox.ProcessPendingAsync(ct: Ct);

        Assert.Equal(new OutboxResult(2, 1, 1), result);
        Assert.Equal(SyncStatus.Failed, await StatusOfAsync(throwing));
        Assert.Equal(SyncStatus.Synced, await StatusOfAsync(good));
    }

    [Fact]
    public async Task Synced_and_failed_scans_are_not_uploaded_by_default()
    {
        await AddAsync(status: SyncStatus.Synced);
        var failed = await AddAsync(status: SyncStatus.Failed);
        var uploader = StubShipmentUploader.AcceptAll();
        using var outbox = CreateOutbox(uploader);

        var result = await outbox.ProcessPendingAsync(ct: Ct);

        Assert.Equal(OutboxResult.Empty, result);
        Assert.Empty(uploader.UploadedIds);
        Assert.Equal(SyncStatus.Failed, await StatusOfAsync(failed));
    }

    [Fact]
    public async Task IncludeFailed_retries_failed_scans_together_with_pending_ones()
    {
        var failed = await AddAsync(minutes: 0, status: SyncStatus.Failed);
        var pending = await AddAsync(minutes: 5);
        await AddAsync(minutes: 10, status: SyncStatus.Synced);
        var uploader = StubShipmentUploader.AcceptAll();
        using var outbox = CreateOutbox(uploader);

        var result = await outbox.ProcessPendingAsync(includeFailed: true, ct: Ct);

        Assert.Equal(new OutboxResult(2, 2, 0), result);
        Assert.Equal([failed.Id, pending.Id], uploader.UploadedIds);
        Assert.Equal(SyncStatus.Synced, await StatusOfAsync(failed));
    }

    [Fact]
    public async Task A_second_run_while_one_is_busy_is_skipped()
    {
        await AddAsync();
        var release = new TaskCompletionSource<UploadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var uploader = new StubShipmentUploader((_, _) =>
        {
            started.TrySetResult();
            return release.Task;
        });
        using var outbox = CreateOutbox(uploader);

        var first = outbox.ProcessPendingAsync(ct: Ct);
        await started.Task.WaitAsync(Ct);
        var second = await outbox.ProcessPendingAsync(ct: Ct);
        release.SetResult(StubShipmentUploader.Accepted);

        Assert.Equal(OutboxResult.Empty, second);
        Assert.Equal(new OutboxResult(1, 1, 0), await first);
        Assert.Single(uploader.UploadedIds);
    }

    [Fact]
    public async Task Cancellation_stops_the_run_and_leaves_unattempted_scans_pending()
    {
        var first = await AddAsync(minutes: 0);
        var second = await AddAsync(minutes: 5);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var uploader = new StubShipmentUploader(async (_, token) =>
        {
            await cts.CancelAsync();
            token.ThrowIfCancellationRequested();
            return StubShipmentUploader.Accepted;
        });
        using var outbox = CreateOutbox(uploader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => outbox.ProcessPendingAsync(ct: cts.Token));

        Assert.Equal([first.Id], uploader.UploadedIds);
        Assert.Equal(SyncStatus.Pending, await StatusOfAsync(first));
        Assert.Equal(SyncStatus.Pending, await StatusOfAsync(second));
    }

    [Fact]
    public async Task The_outbox_can_run_again_after_a_cancelled_run()
    {
        await AddAsync();
        using var outbox = CreateOutbox(StubShipmentUploader.AcceptAll());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => outbox.ProcessPendingAsync(ct: cts.Token));

        var result = await outbox.ProcessPendingAsync(ct: Ct);

        Assert.Equal(new OutboxResult(1, 1, 0), result);
    }

    public void Dispose()
    {
        _database.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
