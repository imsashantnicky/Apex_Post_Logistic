using System.Net;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Tests.Support;
using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.ApiClient.Tests.Resilience;

/// <summary>Retry: transient failures of idempotent requests only, at most three retries.</summary>
public sealed class RetryTests
{
    private const string TrackingNumber = "APX-20000005";
    private const string UploadAnswer = """{ "accepted": 1, "duplicatesSkipped": 0, "rejected": [] }""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_503_is_retried_and_the_later_success_is_returned()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(
            SampleJson.Shipment,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK));

        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, client.Handler.Attempts);
    }

    [Fact]
    public async Task A_lasting_503_is_tried_four_times_then_returned_as_a_server_failure()
    {
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Statuses(SampleJson.Shipment, HttpStatusCode.ServiceUnavailable));

        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.Server, result.Failure.Kind);
        Assert.Equal(503, result.Failure.StatusCode);
        Assert.Equal(1 + ResilientClient.FastSettings.MaxRetryAttempts, client.Handler.Attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Request_timeout_and_every_5xx_are_retried(HttpStatusCode status)
    {
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Statuses(SampleJson.Shipment, status, HttpStatusCode.OK));

        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, client.Handler.Attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_4xx_answer_is_never_retried(HttpStatusCode status)
    {
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Statuses(SampleJson.Shipment, status, HttpStatusCode.OK));

        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)status, result.Failure.StatusCode);
        Assert.Equal(1, client.Handler.Attempts);
        Assert.Empty(client.ResilienceLogs);
    }

    [Fact]
    public async Task A_network_exception_is_retried()
    {
        await using var client = new ResilientClient(
            ScriptedHttpHandler.ThrowingFirst(2, new HttpRequestException("connection refused"), SampleJson.Shipment));

        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, client.Handler.Attempts);
    }

    [Fact]
    public async Task A_status_patch_is_not_idempotent_and_is_sent_once()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(
            SampleJson.Shipment,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK));

        var result = await client.Api.ChangeStatusAsync(TrackingNumber, ShipmentStatus.InTransit, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.Server, result.Failure.Kind);
        Assert.Equal(1, client.Handler.Attempts);
    }

    [Fact]
    public async Task The_marked_scan_upload_is_retried_with_the_same_scan_ids_and_token()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(
            UploadAnswer,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK));
        var scan = new ScanItem(
            "scan::0f8fad5b-d9cb-469f-a165-70867728950e",
            TrackingNumber,
            ScanType.InTransit,
            "CR-101",
            "Central Hub",
            new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc),
            null,
            null);

        var result = await client.Api.UploadScansAsync(TrackingNumber, new ScanUploadRequest([scan]), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, client.Handler.Attempts);
        var bodies = client.Handler.Bodies.ToArray();
        Assert.Contains(scan.ScanId, bodies[0], StringComparison.Ordinal);
        Assert.Equal(bodies[0], bodies[1]);
        Assert.All(client.Handler.Authorizations, header => Assert.Equal($"Bearer {ResilientClient.Token}", header));
    }

    [Fact]
    public async Task A_cancellation_by_the_caller_is_thrown_and_not_retried()
    {
        var settings = new ResilienceSettings
        {
            AttemptTimeout = TimeSpan.FromSeconds(5),
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        };
        await using var client = new ResilientClient(ScriptedHttpHandler.HangingFirst(int.MaxValue, SampleJson.Shipment), settings);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Api.GetShipmentAsync(TrackingNumber, cts.Token));

        Assert.Equal(1, client.Handler.Attempts);
    }
}
