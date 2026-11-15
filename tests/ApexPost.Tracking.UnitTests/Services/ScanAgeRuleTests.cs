using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Tracking.UnitTests.Support;
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Tracking.UnitTests.Services;

/// <summary>
/// TDD rule (Week 5): the server rejects an uploaded scan whose scan time is more than 72 hours before the upload
/// time, with the reason "Scan is older than 72 hours.". See TDD-LOG.md.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ScanAgeRuleTests
{
    private static readonly DateTimeOffset UploadTime = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTracking _store = new();
    private readonly FakeTimeProvider _time = new(UploadTime);
    private readonly ShipmentTrackingService _service;

    public ScanAgeRuleTests()
    {
        _service = new ShipmentTrackingService(_store, _store, _store, new FixedCourier(), _time);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason()
    {
        // Arrange
        _store.Store(TestData.Shipment());
        var scan = ScanAt(new DateTime(2026, 11, 7, 11, 59, 0, DateTimeKind.Utc));

        // Act
        var result = await _service.UploadScansAsync([scan], Ct);

        // Assert
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(scan.ScanId, rejected.ScanId);
        Assert.Equal("Scan is older than 72 hours.", rejected.Reason);
        Assert.Equal(0, result.Accepted);
        Assert.Empty(_store.StoredScans);
    }

    private static ScanUpload ScanAt(DateTime scannedAtUtc) =>
        TestData.Scan(ScanType.Pickup) with { ScannedAtUtc = scannedAtUtc };
}
