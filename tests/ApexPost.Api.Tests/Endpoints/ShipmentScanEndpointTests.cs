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

    private static async Task<ScanUploadResult> UploadAsync(
        HttpClient client,
        string trackingNumber,
        ScanUploadRequest request)
    {
        var response = await client.PostAsJsonAsync(ScansOf(trackingNumber), request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ScanUploadResult>(Ct))!;
    }

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
    public async Task Upload_rejects_a_scan_of_another_shipment_and_stores_the_valid_one()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var other = await CreateShipmentAsync(client, Ct);
        var own = Scan(trackingNumber, ScanType.Pickup);
        var foreign = Scan(other, ScanType.Pickup);
        var request = new ScanUploadRequest { Scans = [own, foreign] };

        var result = await UploadAsync(client, trackingNumber, request);

        Assert.Equal((1, 0), (result.Accepted, result.DuplicatesSkipped));
        Assert.Equal(foreign.ScanId, Assert.Single(result.Rejected).ScanId);
        var stored = await client.GetFromJsonAsync<List<ScanResponse>>(ScansOf(trackingNumber), Ct);
        Assert.Equal(own.ScanId, Assert.Single(stored!).ScanId);
        Assert.Empty((await client.GetFromJsonAsync<List<ScanResponse>>(ScansOf(other), Ct))!);
    }

    [Fact]
    public async Task Upload_of_a_mixed_batch_accepts_the_valid_scans_and_explains_each_rejection()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var other = await CreateShipmentAsync(client, Ct);
        var transit = Scan(trackingNumber, ScanType.InTransit, 50);
        await UploadAsync(client, trackingNumber, new ScanUploadRequest { Scans = [transit] });
        var backward = Scan(trackingNumber, ScanType.Pickup, 40);
        var foreign = Scan(other, ScanType.Pickup, 30);
        var delivered = Scan(trackingNumber, ScanType.Delivered, 20);
        var request = new ScanUploadRequest { Scans = [transit, backward, foreign, delivered, delivered] };

        var result = await UploadAsync(client, trackingNumber, request);

        Assert.Equal((1, 2), (result.Accepted, result.DuplicatesSkipped));
        Assert.Equal([backward.ScanId, foreign.ScanId], result.Rejected.Select(scan => scan.ScanId));
        Assert.All(result.Rejected, scan => Assert.False(string.IsNullOrWhiteSpace(scan.Reason)));
        var shipment = await client.GetFromJsonAsync<ShipmentResponse>($"{Shipments}/{trackingNumber}", Ct);
        Assert.Equal(ShipmentStatus.Delivered, shipment!.Status);
        var stored = await client.GetFromJsonAsync<List<ScanResponse>>(ScansOf(trackingNumber), Ct);
        Assert.Equal([transit.ScanId, delivered.ScanId], stored!.Select(scan => scan.ScanId));
    }

    [Fact]
    public async Task Parallel_uploads_of_the_same_batch_store_each_scan_once()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var request = new ScanUploadRequest
        {
            Scans = [Scan(trackingNumber, ScanType.Pickup, 30), Scan(trackingNumber, ScanType.InTransit, 20)],
        };

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => UploadAsync(client, trackingNumber, request)));

        Assert.Equal(request.Scans.Count, results.Sum(result => result.Accepted));
        Assert.All(results, result => Assert.Empty(result.Rejected));
        Assert.All(results, result => Assert.Equal(request.Scans.Count, result.Accepted + result.DuplicatesSkipped));
        var stored = await client.GetFromJsonAsync<List<ScanResponse>>(ScansOf(trackingNumber), Ct);
        Assert.Equal(request.Scans.Count, stored!.Count);
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
