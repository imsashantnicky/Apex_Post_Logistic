using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Tests.Support;
using ApexPost.Domain.Tracking;
using static ApexPost.Api.Tests.Support.ApiClient;

namespace ApexPost.Api.Tests.Endpoints;

/// <summary>Every status code of the scan endpoints, through the real host and a real SQL Server database.</summary>
public sealed class ShipmentScanEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Unknown = "APX-29999999";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string ScansOf(string trackingNumber) => $"{Shipments}/{trackingNumber}/scans";

    [Fact]
    public async Task Upload_returns_200_with_counts_and_a_repeat_is_skipped_as_duplicates()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var request = new ScanUploadRequest
        {
            Scans = [Scan(trackingNumber, ScanType.Pickup, 30), Scan(trackingNumber, ScanType.InTransit, 20)],
        };

        var first = await client.PostAsJsonAsync(ScansOf(trackingNumber), request, Ct);
        var second = await client.PostAsJsonAsync(ScansOf(trackingNumber), request, Ct);
        var firstResult = await first.Content.ReadFromJsonAsync<ScanUploadResult>(Ct);
        var secondResult = await second.Content.ReadFromJsonAsync<ScanUploadResult>(Ct);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        Assert.Equal((2, 0), (firstResult!.Accepted, firstResult.DuplicatesSkipped));
        Assert.Equal((0, 2), (secondResult!.Accepted, secondResult.DuplicatesSkipped));
        Assert.Empty(secondResult.Rejected);
        var shipment = await client.GetFromJsonAsync<ShipmentResponse>($"{Shipments}/{trackingNumber}", Ct);
        Assert.Equal(ShipmentStatus.InTransit, shipment!.Status);
    }

    [Fact]
    public async Task Upload_with_an_invalid_scan_returns_400()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var body = new { scans = new[] { new { scanId = "not-a-scan-id", trackingNumber } } };

        var problem = await Problem.ReadAsync(
            await client.PostAsJsonAsync(ScansOf(trackingNumber), body, Ct),
            HttpStatusCode.BadRequest);

        Assert.True(problem.Root.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Upload_with_a_scan_of_another_shipment_returns_400_and_stores_nothing()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var other = await CreateShipmentAsync(client, Ct);
        var request = new ScanUploadRequest
        {
            Scans = [Scan(trackingNumber, ScanType.Pickup), Scan(other, ScanType.Pickup)],
        };

        var problem = await Problem.ReadAsync(
            await client.PostAsJsonAsync(ScansOf(trackingNumber), request, Ct),
            HttpStatusCode.BadRequest);

        Assert.True(problem.Root.GetProperty("errors").TryGetProperty("scans[1].trackingNumber", out _));
        Assert.Empty((await client.GetFromJsonAsync<List<ScanResponse>>(ScansOf(trackingNumber), Ct))!);
    }

    [Fact]
    public async Task Upload_for_an_unknown_shipment_returns_404()
    {
        using var client = factory.CreateClient();
        var request = new ScanUploadRequest { Scans = [Scan(Unknown, ScanType.Pickup)] };

        await Problem.ReadAsync(
            await client.PostAsJsonAsync(ScansOf(Unknown), request, Ct),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_with_a_wrong_content_type_returns_415()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var request = new ScanUploadRequest { Scans = [Scan(trackingNumber, ScanType.Pickup)] };
        var json = JsonSerializer.Serialize(request, Web);

        await Problem.ReadAsync(
            await client.PostAsync(ScansOf(trackingNumber), Body(json, "application/xml"), Ct),
            HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task List_returns_200_with_the_scans_ordered_by_scannedAtUtc()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var pickup = Scan(trackingNumber, ScanType.Pickup, 60);
        var transit = Scan(trackingNumber, ScanType.InTransit, 40);
        var delivered = Scan(trackingNumber, ScanType.Delivered, 10);
        var request = new ScanUploadRequest { Scans = [delivered, pickup, transit] };
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(ScansOf(trackingNumber), request, Ct)).StatusCode);

        var response = await client.GetAsync(ScansOf(trackingNumber), Ct);
        var scans = await response.Content.ReadFromJsonAsync<List<ScanResponse>>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(scans);
        Assert.Equal([pickup.ScanId, transit.ScanId, delivered.ScanId], scans.Select(scan => scan.ScanId));
        Assert.True(scans.SequenceEqual(scans.OrderBy(scan => scan.ScannedAtUtc)));
    }

    [Fact]
    public async Task List_of_an_unknown_shipment_returns_404()
    {
        using var client = factory.CreateClient();

        await Problem.ReadAsync(await client.GetAsync(ScansOf(Unknown), Ct), HttpStatusCode.NotFound);
    }
}
