using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Tracking.UnitTests.Support;
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Tracking.UnitTests.Services;

/// <summary>
/// TDD rule (Week 5): the server rejects an uploaded scan whose scan time is more than 72 hours before the upload
/// time, with the reason "Scan is older than 72 hours."; the limit is <see cref="TrackingOptions.MaxScanAgeHours"/>.
/// See TDD-LOG.md.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ScanAgeRuleTests
{
    private const string TooOldReason = "Scan is older than 72 hours.";

    private static readonly DateTimeOffset UploadTime = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTracking _store = new();
    private readonly FakeTimeProvider _time = new(UploadTime);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason()
    {
        // Arrange
        var scan = ScanAt(new DateTime(2026, 11, 7, 11, 59, 0, DateTimeKind.Utc));

        // Act
        var result = await UploadAsync(scan);

        // Assert
        AssertRejected(result, scan, TooOldReason);
    }

    [Fact]
    public async Task UploadScansAsync_ScanExactly72HoursOld_AcceptsScan()
    {
        // Arrange
        var scan = ScanAt(new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await UploadAsync(scan);

        // Assert
        AssertAccepted(result, scan);
    }

    [Fact]
    public async Task UploadScansAsync_Scan72HoursAndOneSecondOld_RejectsScanWithReason()
    {
        // Arrange
        var scan = ScanAt(new DateTime(2026, 11, 7, 11, 59, 59, DateTimeKind.Utc));

        // Act
        var result = await UploadAsync(scan);

        // Assert
        AssertRejected(result, scan, TooOldReason);
    }

    [Theory]
    [InlineData(24, 0, null)]
    [InlineData(24, 1, "Scan is older than 24 hours.")]
    [InlineData(96, 0, null)]
    [InlineData(96, 1, "Scan is older than 96 hours.")]
    public async Task UploadScansAsync_ConfiguredLimit_AcceptsAtLimitAndRejectsOneSecondOlder(
        int maxScanAgeHours,
        int secondsOverLimit,
        string? expectedReason)
    {
        // Arrange
        var scan = ScanAt(UploadTime.UtcDateTime.AddHours(-maxScanAgeHours).AddSeconds(-secondsOverLimit));

        // Act
        var result = await UploadAsync(scan, maxScanAgeHours);

        // Assert
        if (expectedReason is null)
        {
            AssertAccepted(result, scan);
        }
        else
        {
            AssertRejected(result, scan, expectedReason);
        }
    }

    private static ScanUpload ScanAt(DateTime scannedAtUtc) =>
        TestData.Scan(ScanType.Pickup) with { ScannedAtUtc = scannedAtUtc };

    private Task<ScanUploadResult> UploadAsync(
        ScanUpload scan,
        int maxScanAgeHours = TrackingOptions.DefaultMaxScanAgeHours)
    {
        _store.Store(TestData.Shipment());
        var service = new ShipmentTrackingService(
            _store, _store, _store, new FixedCourier(), _time, TestData.ScanAge(maxScanAgeHours));
        return service.UploadScansAsync([scan], Ct);
    }

    private void AssertAccepted(ScanUploadResult result, ScanUpload scan)
    {
        Assert.Equal(1, result.Accepted);
        Assert.Empty(result.Rejected);
        Assert.Equal(scan.ScanId, Assert.Single(_store.StoredScans).ScanId);
    }

    private void AssertRejected(ScanUploadResult result, ScanUpload scan, string reason)
    {
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(scan.ScanId, rejected.ScanId);
        Assert.Equal(reason, rejected.Reason);
        Assert.Equal(0, result.Accepted);
        Assert.Empty(_store.StoredScans);
    }
}
