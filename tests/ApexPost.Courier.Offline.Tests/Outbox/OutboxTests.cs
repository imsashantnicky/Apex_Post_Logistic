using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.Offline.Tests.Outbox;

public sealed class OutboxTests : IDisposable
{
    private readonly TempCourierDatabase _db = new();
    private readonly OutboxService _outbox;

    public OutboxTests()
    {
        _outbox = new OutboxService(
            _db.Repository, _db.Repository, new FakeShipmentUploader(), NullLogger<OutboxService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Accepted_scans_become_synced_and_the_demo_failure_becomes_failed()
    {
        var accepted = TestScans.At(0);
        var rejected = TestScans.At(1, trackingNumber: FakeShipmentUploader.DemoFailureTrackingNumber);
        var alsoAccepted = TestScans.At(2, trackingNumber: "APX-10000002");
        foreach (var scan in new[] { accepted, rejected, alsoAccepted })
        {
            await _db.Repository.CreateAsync(scan, Ct);
        }

        var result = await _outbox.ProcessPendingAsync(ct: Ct);

        Assert.Equal(new OutboxResult(3, 2, 1), result);
        Assert.Equal(SyncStatus.Synced, (await _db.Repository.GetByIdAsync(accepted.Id, Ct))?.SyncStatus);
        Assert.Equal(SyncStatus.Synced, (await _db.Repository.GetByIdAsync(alsoAccepted.Id, Ct))?.SyncStatus);
        Assert.Equal(SyncStatus.Failed, (await _db.Repository.GetByIdAsync(rejected.Id, Ct))?.SyncStatus);
        Assert.Equal(0, await _db.Repository.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task Synced_scans_are_not_uploaded_again_and_failed_ones_only_on_retry()
    {
        await _db.Repository.CreateAsync(TestScans.At(0, syncStatus: SyncStatus.Synced), Ct);
        await _db.Repository.CreateAsync(
            TestScans.At(1, trackingNumber: FakeShipmentUploader.DemoFailureTrackingNumber,
                syncStatus: SyncStatus.Failed), Ct);

        Assert.Equal(OutboxResult.Empty, await _outbox.ProcessPendingAsync(includeFailed: false, Ct));
        Assert.Equal(new OutboxResult(1, 0, 1), await _outbox.ProcessPendingAsync(includeFailed: true, Ct));
    }

    public void Dispose()
    {
        _outbox.Dispose();
        _db.Dispose();
    }
}
