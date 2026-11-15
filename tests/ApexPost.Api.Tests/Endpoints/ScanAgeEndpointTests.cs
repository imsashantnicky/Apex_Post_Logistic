using System.Net;
using System.Net.Http.Json;
using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Tests.Support;
using ApexPost.Domain.Tracking;
using Microsoft.AspNetCore.Hosting;
using static ApexPost.Api.Tests.Support.ApiClient;
using TrackingOptions = ApexPost.Application.Tracking.TrackingOptions;

namespace ApexPost.Api.Tests.Endpoints;

/// <summary>
/// The scan age rule (Week 5 TDD, see TDD-LOG.md) as a caller sees it: through the real host, its fixed clock and a
/// throw-away SQL Server database, an old scan is answered with 200 and a rejection reason, and the
/// "Tracking:MaxScanAgeHours" setting of the host changes the limit.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ScanAgeEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int MinutesPerHour = 60;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UploadScans_ScanOlderThan72Hours_Returns200WithRejectedReasonAndStoresOnlyTheFreshScan()
    {
        // Arrange
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var old = Scan(trackingNumber, ScanType.Pickup, minutesAgo: 73 * MinutesPerHour);
        var fresh = Scan(trackingNumber, ScanType.Pickup, minutesAgo: 30);

        // Act
        using var response = await client.PostAsJsonAsync(
            $"{Shipments}/{trackingNumber}/scans", new ScanUploadRequest { Scans = [old, fresh] }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ScanUploadResult>(Web, Ct);
        Assert.Equal((1, 0), (result!.Accepted, result.DuplicatesSkipped));
        Assert.Equal(
            new RejectedScanResponse(old.ScanId, "Scan is older than 72 hours."),
            Assert.Single(result.Rejected));
        var stored = await client.GetFromJsonAsync<List<ScanResponse>>($"{Shipments}/{trackingNumber}/scans", Web, Ct);
        Assert.Equal(fresh.ScanId, Assert.Single(stored!).ScanId);
    }

    [Fact]
    public async Task UploadScans_HostLimitSetTo24Hours_RejectsScanOlderThan24Hours()
    {
        // Arrange
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.UseSetting($"{TrackingOptions.SectionName}:{nameof(TrackingOptions.MaxScanAgeHours)}", "24"));
        using var client = host.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var tooOld = Scan(trackingNumber, ScanType.Pickup, minutesAgo: 25 * MinutesPerHour);
        var young = Scan(trackingNumber, ScanType.Pickup, minutesAgo: 23 * MinutesPerHour);

        // Act
        using var response = await client.PostAsJsonAsync(
            $"{Shipments}/{trackingNumber}/scans", new ScanUploadRequest { Scans = [tooOld, young] }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ScanUploadResult>(Web, Ct);
        Assert.Equal(1, result!.Accepted);
        Assert.Equal(
            new RejectedScanResponse(tooOld.ScanId, "Scan is older than 24 hours."),
            Assert.Single(result.Rejected));
    }
}
