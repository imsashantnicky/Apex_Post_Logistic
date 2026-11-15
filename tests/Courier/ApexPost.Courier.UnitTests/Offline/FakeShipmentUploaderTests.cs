using System.Diagnostics;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Outbox;

namespace ApexPost.Courier.UnitTests.Offline;

public sealed class FakeShipmentUploaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ShipmentScan Scan(string trackingNumber = "APX-10000001") =>
        ShipmentScan.CreateNew(trackingNumber, ScanType.Pickup, "CR-101", "BR001", null, TimeProvider.System);

    [Fact]
    public async Task Upload_succeeds_after_a_simulated_network_delay()
    {
        var uploader = new FakeShipmentUploader();
        var watch = Stopwatch.StartNew();

        var result = await uploader.UploadAsync(Scan(), Ct);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.True(watch.Elapsed >= FakeShipmentUploader.MinDelay - TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public async Task Upload_of_the_demo_failure_tracking_number_always_fails()
    {
        var uploader = new FakeShipmentUploader();

        var result = await uploader.UploadAsync(Scan(FakeShipmentUploader.DemoFailureTrackingNumber), Ct);

        Assert.False(result.Success);
        Assert.Contains(FakeShipmentUploader.DemoFailureTrackingNumber, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Upload_with_failure_rate_one_always_fails()
    {
        var uploader = new FakeShipmentUploader(failureRate: 1, seed: 42);

        var result = await uploader.UploadAsync(Scan(), Ct);

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Failure_rate_outside_zero_to_one_is_rejected(double failureRate) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FakeShipmentUploader(failureRate));

    [Fact]
    public async Task Upload_honours_cancellation_during_the_delay()
    {
        var uploader = new FakeShipmentUploader();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uploader.UploadAsync(Scan(), cts.Token));
    }
}
