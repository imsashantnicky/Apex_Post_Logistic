using System.Net;
using System.Text.Json;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Sync;
using ApexPost.Courier.ApiClient.Tests.Support;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.ApiClient.Tests.Sync;

/// <summary>The HTTP uploader through the full client pipeline (auth, retry, breaker) with a scripted server.</summary>
public sealed class HttpShipmentUploaderTests
{
    private const string TrackingNumber = "APX-20000005";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_batch_is_posted_to_the_scans_route_with_every_field_and_the_token()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(Answer(), HttpStatusCode.OK));
        var scans = new[] { Scan(0, ScanType.Pickup, "first pickup"), Scan(5, ScanType.InTransit, null) };

        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, scans, Ct);

        Assert.Equal(BatchUploadOutcome.Completed, result.Outcome);
        Assert.Empty(result.Rejected);
        Assert.Equal($"Bearer {ResilientClient.Token}", Assert.Single(client.Handler.Authorizations));
        using var body = JsonDocument.Parse(Assert.Single(client.Handler.Bodies)!);
        var items = body.RootElement.GetProperty("scans");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(scans[0].Id, items[0].GetProperty("scanId").GetString());
        Assert.Equal(TrackingNumber, items[0].GetProperty("trackingNumber").GetString());
        Assert.Equal("Pickup", items[0].GetProperty("scanType").GetString());
        Assert.Equal("CR-101", items[0].GetProperty("courierId").GetString());
        Assert.Equal("Central Hub", items[0].GetProperty("location").GetString());
        Assert.Equal("2026-11-05T08:00:00Z", items[0].GetProperty("scannedAtUtc").GetString());
        Assert.Equal("first pickup", items[0].GetProperty("notes").GetString());
        Assert.Equal("InTransit", items[1].GetProperty("scanType").GetString());
    }

    [Fact]
    public async Task Rejected_scans_come_back_with_their_reasons()
    {
        var scans = new[] { Scan(0, ScanType.Pickup, null), Scan(5, ScanType.InTransit, null) };
        var answer = Answer(accepted: 1, rejected: (scans[1].Id, "Status cannot move back."));
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(answer, HttpStatusCode.OK));

        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, scans, Ct);

        Assert.Equal(BatchUploadOutcome.Completed, result.Outcome);
        Assert.Equal("Status cannot move back.", Assert.Single(result.Rejected, pair => pair.Key == scans[1].Id).Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, BatchUploadOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest, BatchUploadOutcome.Refused)]
    [InlineData(HttpStatusCode.Forbidden, BatchUploadOutcome.Refused)]
    [InlineData(HttpStatusCode.NotFound, BatchUploadOutcome.Refused)]
    [InlineData(HttpStatusCode.TooManyRequests, BatchUploadOutcome.Retryable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, BatchUploadOutcome.Retryable)]
    public async Task An_error_status_maps_to_what_the_outbox_does(HttpStatusCode status, BatchUploadOutcome expected)
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(Answer(), status));

        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.Pickup, null)], Ct);

        Assert.Equal(expected, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task A_network_failure_is_retryable_and_the_upload_was_retried_first()
    {
        await using var client = new ResilientClient(new ScriptedHttpHandler(
            (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("no route"))));

        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.Pickup, null)], Ct);

        Assert.Equal(BatchUploadOutcome.Retryable, result.Outcome);
        Assert.Equal(1 + ResilientClient.FastSettings.MaxRetryAttempts, client.Handler.Attempts);
    }

    [Fact]
    public async Task While_the_circuit_is_open_the_batch_is_retryable_and_nothing_is_sent()
    {
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Statuses(Answer(), HttpStatusCode.ServiceUnavailable));
        await client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.Pickup, null)], Ct);
        var attempts = client.Handler.Attempts;

        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(1, ScanType.Pickup, null)], Ct);

        Assert.Equal(BatchUploadOutcome.Retryable, result.Outcome);
        Assert.Equal(FailureMessages.ServerUnavailable, result.Error);
        Assert.Equal(attempts, client.Handler.Attempts);
    }

    [Fact]
    public async Task The_single_scan_port_reports_success_and_rejection()
    {
        var scan = Scan(0, ScanType.Pickup, null);
        await using var accepting = new ResilientClient(ScriptedHttpHandler.Statuses(Answer(), HttpStatusCode.OK));
        await using var rejecting = new ResilientClient(ScriptedHttpHandler.Statuses(
            Answer(accepted: 0, rejected: (scan.Id, "Unknown shipment.")),
            HttpStatusCode.OK));

        Assert.Equal(new UploadResult(true, null), await accepting.Uploader.UploadAsync(scan, Ct));
        Assert.Equal(new UploadResult(false, "Unknown shipment."), await rejecting.Uploader.UploadAsync(scan, Ct));
    }

    [Fact]
    public async Task Scans_of_another_shipment_or_too_many_scans_are_refused_before_sending()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(Answer(), HttpStatusCode.OK));
        var other = ShipmentScan.Restore(
            ScanIds.New(), "APX-20000006", ScanType.Pickup, "CR-101", "Central Hub",
            new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero), null, SyncStatus.Pending);
        var tooMany = Enumerable.Range(0, HttpShipmentUploader.ApiMaxBatchSize + 1)
            .Select(minute => Scan(minute, ScanType.InTransit, null))
            .ToArray();

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.Pickup, null), other], Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.Uploader.UploadBatchAsync(TrackingNumber, tooMany, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.Uploader.UploadBatchAsync(TrackingNumber, [], Ct));
        Assert.Equal(0, client.Handler.Attempts);
    }

    [Theory]
    [InlineData(ApiFailureKind.Unauthorized, null, BatchUploadOutcome.Unauthorized)]
    [InlineData(ApiFailureKind.Network, null, BatchUploadOutcome.Retryable)]
    [InlineData(ApiFailureKind.Timeout, null, BatchUploadOutcome.Retryable)]
    [InlineData(ApiFailureKind.Server, 500, BatchUploadOutcome.Retryable)]
    [InlineData(ApiFailureKind.ServerUnavailable, null, BatchUploadOutcome.Retryable)]
    [InlineData(ApiFailureKind.InvalidResponse, 200, BatchUploadOutcome.Retryable)]
    [InlineData(ApiFailureKind.Rejected, 429, BatchUploadOutcome.Retryable)]
    [InlineData(ApiFailureKind.Rejected, 415, BatchUploadOutcome.Refused)]
    [InlineData(ApiFailureKind.Validation, 400, BatchUploadOutcome.Refused)]
    [InlineData(ApiFailureKind.Forbidden, 403, BatchUploadOutcome.Refused)]
    [InlineData(ApiFailureKind.NotFound, 404, BatchUploadOutcome.Refused)]
    [InlineData(ApiFailureKind.InvalidInput, null, BatchUploadOutcome.Refused)]
    public void Every_failure_kind_has_an_outbox_outcome(ApiFailureKind kind, int? status, BatchUploadOutcome expected)
    {
        var result = HttpShipmentUploader.FromFailure(new ApiFailure(kind, "reason", status));

        Assert.Equal(expected, result.Outcome);
        Assert.Equal("reason", result.Error);
    }

    private static ShipmentScan Scan(int minutes, ScanType type, string? notes) => ShipmentScan.Restore(
        ScanIds.New(),
        TrackingNumber,
        type,
        "CR-101",
        "Central Hub",
        new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero).AddMinutes(minutes),
        notes,
        SyncStatus.Pending);

    private static string Answer(int accepted = 1, params (string ScanId, string Reason)[] rejected) =>
        JsonSerializer.Serialize(new
        {
            accepted,
            duplicatesSkipped = 0,
            rejected = rejected.Select(item => new { scanId = item.ScanId, reason = item.Reason }),
        });
}
