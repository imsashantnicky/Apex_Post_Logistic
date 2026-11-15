using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.Offline.Tests.Outbox;

/// <summary>The outbox with a batch uploader: batches of 20 per tracking number and the status of every outcome.</summary>
public sealed class BatchOutboxTests : IDisposable
{
    private readonly TempCourierDatabase _db = new();
    private readonly List<OutboxService> _outboxes = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Scans_are_sent_in_batches_of_twenty_per_tracking_number_oldest_first()
    {
        await AddScansAsync(45, "APX-10000002", startMinute: 100);
        await AddScansAsync(3, "APX-10000001", startMinute: 0);
        var uploader = ScriptedBatchUploader.AcceptingAll();

        var result = await Outbox(uploader).ProcessPendingAsync(ct: Ct);

        Assert.Equal(new OutboxResult(48, 48, 0), result);
        var batches = uploader.Batches.ToArray();
        Assert.Equal([3, 20, 20, 5], batches.Select(batch => batch.Count));
        Assert.All(batches[0], scan => Assert.Equal("APX-10000001", scan.TrackingNumber));
        Assert.All(batches.Skip(1).SelectMany(batch => batch), scan => Assert.Equal("APX-10000002", scan.TrackingNumber));
        var sent = batches.Skip(1).SelectMany(batch => batch).Select(scan => scan.ScannedAtUtc).ToList();
        Assert.Equal(sent.Order(), sent);
        Assert.Equal(0, await _db.Repository.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task The_uploader_limit_wins_when_it_is_below_twenty()
    {
        await AddScansAsync(7, "APX-10000001", startMinute: 0);
        var uploader = new ScriptedBatchUploader((_, _) => BatchUploadResult.Completed(new Dictionary<string, string>()))
        {
            MaxBatchSize = 3,
        };

        await Outbox(uploader).ProcessPendingAsync(ct: Ct);

        Assert.Equal([3, 3, 1], uploader.Batches.Select(batch => batch.Count));
    }

    [Fact]
    public async Task Accepted_and_duplicate_scans_become_synced_and_rejected_ones_failed()
    {
        var scans = await AddScansAsync(3, "APX-10000001", startMinute: 0);
        var uploader = new ScriptedBatchUploader((_, _) => BatchUploadResult.Completed(
            new Dictionary<string, string> { [scans[1].Id] = "Status cannot move back." }));

        var result = await Outbox(uploader).ProcessPendingAsync(ct: Ct);

        Assert.Equal(new OutboxResult(3, 2, 1), result);
        Assert.Equal(SyncStatus.Synced, await StatusAsync(scans[0]));
        Assert.Equal(SyncStatus.Failed, await StatusAsync(scans[1]));
        Assert.Equal(SyncStatus.Synced, await StatusAsync(scans[2]));
    }

    [Fact]
    public async Task A_refused_batch_marks_its_scans_failed_and_the_run_goes_on()
    {
        var refused = await AddScansAsync(2, "APX-10000001", startMinute: 0);
        var accepted = await AddScansAsync(1, "APX-10000002", startMinute: 10);
        var uploader = new ScriptedBatchUploader((batch, _) => batch == 1
            ? BatchUploadResult.Refused("No shipment was found for that tracking number.")
            : BatchUploadResult.Completed(new Dictionary<string, string>()));

        var result = await Outbox(uploader).ProcessPendingAsync(ct: Ct);

        Assert.Equal(new OutboxResult(3, 1, 2), result);
        Assert.All(refused, scan => Assert.Equal(SyncStatus.Failed, StatusAsync(scan).Result));
        Assert.Equal(SyncStatus.Synced, await StatusAsync(accepted[0]));
    }

    [Fact]
    public async Task A_network_failure_or_open_circuit_keeps_the_scans_pending_and_stops_the_run()
    {
        var synced = await AddScansAsync(1, "APX-10000001", startMinute: 0);
        var waiting = await AddScansAsync(25, "APX-10000002", startMinute: 10);
        var uploader = new ScriptedBatchUploader((batch, _) => batch == 1
            ? BatchUploadResult.Completed(new Dictionary<string, string>())
            : BatchUploadResult.Retryable("Server unavailable – working offline."));

        var result = await Outbox(uploader).ProcessPendingAsync(ct: Ct);

        Assert.Equal(new OutboxResult(26, 1, 0) { Deferred = 25, StopReason = "Server unavailable – working offline." }, result);
        Assert.Equal(2, uploader.Batches.Count);
        Assert.Equal(SyncStatus.Synced, await StatusAsync(synced[0]));
        Assert.Equal(25, await _db.Repository.CountPendingAsync(Ct));
        Assert.All(waiting, scan => Assert.Equal(SyncStatus.Pending, StatusAsync(scan).Result));
    }

    [Fact]
    public async Task A_401_keeps_the_scans_pending_and_asks_for_a_new_sign_in()
    {
        await AddScansAsync(2, "APX-10000001", startMinute: 0);
        var uploader = new ScriptedBatchUploader((_, _) => BatchUploadResult.Unauthorized("Sign in again."));

        var result = await Outbox(uploader).ProcessPendingAsync(ct: Ct);

        Assert.True(result.SignInRequired);
        Assert.Equal(2, result.Deferred);
        Assert.Equal(2, await _db.Repository.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task An_exception_from_the_uploader_keeps_the_scans_pending()
    {
        await AddScansAsync(2, "APX-10000001", startMinute: 0);
        var uploader = new ScriptedBatchUploader((_, _) => throw new InvalidOperationException("boom"));

        var result = await Outbox(uploader).ProcessPendingAsync(ct: Ct);

        Assert.Equal(2, result.Deferred);
        Assert.False(result.SignInRequired);
        Assert.Equal(2, await _db.Repository.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task Failed_scans_are_retried_in_batches_only_when_asked()
    {
        await AddScansAsync(1, "APX-10000001", startMinute: 0, SyncStatus.Failed);
        var uploader = ScriptedBatchUploader.AcceptingAll();
        var outbox = Outbox(uploader);

        Assert.Equal(OutboxResult.Empty, await outbox.ProcessPendingAsync(includeFailed: false, Ct));
        Assert.Equal(new OutboxResult(1, 1, 0), await outbox.ProcessPendingAsync(includeFailed: true, Ct));
    }

    [Fact]
    public async Task A_second_run_while_one_is_busy_returns_at_once()
    {
        await AddScansAsync(1, "APX-10000001", startMinute: 0);
        using var release = new ManualResetEventSlim();
        var uploader = new ScriptedBatchUploader((_, _) =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            return BatchUploadResult.Completed(new Dictionary<string, string>());
        });
        var outbox = Outbox(uploader);

        var first = Task.Run(() => outbox.ProcessPendingAsync(ct: Ct), Ct);
        await WaitUntilAsync(() => !uploader.Batches.IsEmpty);
        var second = await outbox.ProcessPendingAsync(ct: Ct);
        release.Set();

        Assert.Equal(OutboxResult.Empty, second);
        Assert.Equal(new OutboxResult(1, 1, 0), await first);
    }

    [Fact]
    public void BuildBatches_rejects_a_batch_size_below_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OutboxService.BuildBatches([], 0));
        Assert.Empty(OutboxService.BuildBatches([], OutboxService.BatchSize));
    }

    public void Dispose()
    {
        _outboxes.ForEach(outbox => outbox.Dispose());
        _db.Dispose();
    }

    private OutboxService Outbox(IBatchShipmentUploader uploader)
    {
        var outbox = new OutboxService(_db.Repository, _db.Repository, uploader, NullLogger<OutboxService>.Instance);
        _outboxes.Add(outbox);
        return outbox;
    }

    private async Task<IReadOnlyList<ShipmentScan>> AddScansAsync(
        int count,
        string trackingNumber,
        int startMinute,
        SyncStatus status = SyncStatus.Pending)
    {
        var scans = new List<ShipmentScan>();
        for (var index = 0; index < count; index++)
        {
            // Stored newest first, so the ordering must come from the outbox, not from insertion order.
            var scan = TestScans.At(startMinute + count - index, trackingNumber, syncStatus: status);
            await _db.Repository.CreateAsync(scan, Ct);
            scans.Insert(0, scan);
        }

        return scans;
    }

    private async Task<SyncStatus?> StatusAsync(ShipmentScan scan) =>
        (await _db.Repository.GetByIdAsync(scan.Id, Ct))?.SyncStatus;

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10, Ct);
        }

        Assert.True(condition());
    }
}
