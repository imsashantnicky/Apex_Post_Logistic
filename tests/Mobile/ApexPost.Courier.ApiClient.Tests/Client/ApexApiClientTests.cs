using System.Net;
using System.Text.Json;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Tests.Support;
using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.ApiClient.Tests.Client;

public sealed class ApexApiClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetShipment_reads_the_v2_shipment_with_string_enums_and_nested_recipient()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SampleJson.Shipment, SampleJson.Json);

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-20000005", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.InTransit, result.Value.Status);
        Assert.Equal("Northgate", result.Value.Recipient.City);
        Assert.Equal(ScanType.InTransit, result.Value.LatestScan?.ScanType);
        Assert.Equal(DateTimeKind.Utc, result.Value.CreatedAtUtc.Kind);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://localhost:7310/api/v2/shipments/APX-20000005", sent.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task GetShipment_accepts_a_null_latest_scan()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SampleJson.ShipmentWithoutScan, SampleJson.Json);

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-20000001", Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.LatestScan);
    }

    [Theory]
    [InlineData("")]
    [InlineData("APX-1234")]
    [InlineData("apx-12345678")]
    [InlineData("APX-12345678/../../admin")]
    public async Task An_invalid_tracking_number_fails_without_sending_a_request(string trackingNumber)
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SampleJson.Shipment, SampleJson.Json);

        var result = await ClientFactory.Create(handler).GetShipmentAsync(trackingNumber, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.InvalidInput, result.Failure.Kind);
        Assert.Equal(FailureMessages.InvalidTrackingNumber, result.Failure.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UploadScans_posts_camel_case_json_with_string_scan_type_to_the_scans_route()
    {
        const string answer = """{ "accepted": 1, "duplicatesSkipped": 1, "rejected": [] }""";
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, answer, SampleJson.Json);
        var scan = new ScanItem(
            "scan::0f8fad5b-d9cb-469f-a165-70867728950e",
            "APX-20000005",
            ScanType.Delivered,
            "CR-101",
            "Northgate",
            new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc),
            null,
            "Mia Example");

        var result = await ClientFactory.Create(handler).UploadScansAsync("APX-20000005", new ScanUploadRequest([scan]), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Accepted);
        Assert.Equal(1, result.Value.DuplicatesSkipped);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("/api/v2/shipments/APX-20000005/scans", sent.Uri.AbsolutePath);
        Assert.Equal("application/json", sent.ContentType);
        using var body = JsonDocument.Parse(sent.Body!);
        var item = body.RootElement.GetProperty("scans")[0];
        Assert.Equal("Delivered", item.GetProperty("scanType").GetString());
        Assert.Equal("CR-101", item.GetProperty("courierId").GetString());
        Assert.Equal("2026-11-05T10:00:00Z", item.GetProperty("scannedAtUtc").GetString());
        Assert.Equal("Mia Example", item.GetProperty("recipientSignatureName").GetString());
    }

    [Fact]
    public async Task ChangeStatus_sends_a_patch_with_the_status_as_a_string()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SampleJson.Shipment, SampleJson.Json);

        var result = await ClientFactory.Create(handler).ChangeStatusAsync("APX-20000005", ShipmentStatus.InTransit, Ct);

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, sent.Method);
        Assert.Equal("/api/v2/shipments/APX-20000005/status", sent.Uri.AbsolutePath);
        Assert.Equal("""{"status":"InTransit"}""", sent.Body);
    }

    [Fact]
    public async Task ListShipments_sends_page_and_page_size_and_reads_the_page()
    {
        var page = $$"""{ "items": [ {{SampleJson.Shipment}} ], "page": 2, "pageSize": 1, "totalCount": 3, "totalPages": 3, "hasNext": true }""";
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, page, SampleJson.Json);

        var result = await ClientFactory.Create(handler).ListShipmentsAsync(2, 1, Ct);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.True(result.Value.HasNext);
        Assert.Equal("?page=2&pageSize=1", Assert.Single(handler.Requests).Uri.Query);
    }

    [Fact]
    public async Task GetScans_reads_the_scan_array()
    {
        const string scans = """
            [ { "scanId": "scan::1", "trackingNumber": "APX-20000005", "scanType": "Pickup", "courierId": "CR-101",
                "location": "Depot", "scannedAtUtc": "2026-11-05T08:30:00Z", "notes": null,
                "receivedAtUtc": "2026-11-05T08:31:00Z" } ]
            """;
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, scans, SampleJson.Json);

        var result = await ClientFactory.Create(handler).GetScansAsync("APX-20000005", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ScanType.Pickup, Assert.Single(result.Value).ScanType);
        Assert.Equal("/api/v2/shipments/APX-20000005/scans", Assert.Single(handler.Requests).Uri.AbsolutePath);
    }

    [Fact]
    public async Task A_network_error_becomes_a_network_failure()
    {
        var handler = FakeHttpMessageHandler.Throwing(new HttpRequestException("No route to host"));

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-20000005", Ct);

        Assert.Equal(ApiFailureKind.Network, result.Failure?.Kind);
        Assert.Equal(FailureMessages.Network, result.Failure?.Message);
        Assert.Null(result.Failure?.StatusCode);
    }

    [Fact]
    public async Task An_http_client_timeout_becomes_a_timeout_failure()
    {
        var handler = FakeHttpMessageHandler.Throwing(new TaskCanceledException("HttpClient.Timeout elapsed"));

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-20000005", Ct);

        Assert.Equal(ApiFailureKind.Timeout, result.Failure?.Kind);
    }

    [Fact]
    public async Task A_cancellation_requested_by_the_caller_is_thrown_not_reported_as_a_failure()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SampleJson.Shipment, SampleJson.Json);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ClientFactory.Create(handler).GetShipmentAsync("APX-20000005", cts.Token));
    }
}
