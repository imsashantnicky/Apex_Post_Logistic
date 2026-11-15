using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Sync;
using ApexPost.Courier.ApiClient.Tests.Support;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Protected;

namespace ApexPost.Courier.ApiClient.Tests.Sync;

/// <summary>
/// One test per HTTP case for <see cref="HttpShipmentUploader"/>: what the outbox gets back for each server answer.
/// The uploader runs through the real registration (auth handler + resilience pipeline) on a fake or Moq
/// <see cref="HttpMessageHandler"/> behind the <see cref="NetworkGuardHandler"/>; retries have zero delay and
/// timeouts run on a <see cref="FakeTimeProvider"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpShipmentUploaderHttpHandlerTests
{
    private const string TrackingNumber = "APX-20000005";

    private static readonly DateTimeOffset Start = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private static int AllAttempts => 1 + ResilientClient.NoDelaySettings.MaxRetryAttempts;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UploadBatchAsync_Http200WithV2Json_CompletesWithRejectedReasonsAndSendsStringEnums()
    {
        // Arrange
        var scans = new[] { Scan(0, ScanType.Pickup), Scan(5, ScanType.InTransit) };
        var answer = $$"""
            { "accepted": 1, "duplicatesSkipped": 0,
              "rejected": [ { "scanId": "{{scans[1].Id}}", "reason": "Status cannot move back." } ] }
            """;
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Statuses(answer, HttpStatusCode.OK), ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, scans, Ct);

        // Assert
        Assert.Equal(BatchUploadOutcome.Completed, result.Outcome);
        Assert.Equal("Status cannot move back.", Assert.Single(result.Rejected, pair => pair.Key == scans[1].Id).Value);
        Assert.Equal(1, client.Handler.Attempts);
        using var body = JsonDocument.Parse(Assert.Single(client.Handler.Bodies)!);
        Assert.Equal("Pickup", body.RootElement.GetProperty("scans")[0].GetProperty("scanType").GetString());
        Assert.Equal("InTransit", body.RootElement.GetProperty("scans")[1].GetProperty("scanType").GetString());
    }

    [Fact]
    public async Task UploadBatchAsync_Http404ProblemDetails_RefusedWithServerDetailAndNoRetry()
    {
        // Arrange
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Problem(HttpStatusCode.NotFound, SampleJson.NotFoundProblem),
            ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.Pickup)], Ct);

        // Assert
        Assert.Equal(BatchUploadOutcome.Refused, result.Outcome);
        Assert.Equal("Shipment APX-00000000 was not found.", result.Error);
        Assert.Equal(1, client.Handler.Attempts);
    }

    [Fact]
    public async Task UploadBatchAsync_Http500OnEveryAttempt_RetriesPerPolicyThenRetryableWithFriendlyText()
    {
        // Arrange
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Problem(HttpStatusCode.InternalServerError, SampleJson.ServerProblem),
            ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.Pickup)], Ct);

        // Assert
        Assert.Equal(BatchUploadOutcome.Retryable, result.Outcome);
        Assert.Equal("The ApexPost server had a problem. Try again later.", result.Error);
        Assert.Equal(AllAttempts, client.Handler.Attempts);
        Assert.Single(client.Handler.Bodies.Distinct());
    }

    [Fact]
    public async Task UploadBatchAsync_EveryAttemptTimesOut_RetriesPerPolicyThenRetryableTimeout()
    {
        // Arrange
        var time = new FakeTimeProvider(Start);
        var handler = ScriptedHttpHandler.Hanging();
        await using var client = new ResilientClient(handler, ResilientClient.NoDelaySettings, time);

        // Act
        var upload = client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.Pickup)], Ct);
        for (var attempt = 1; attempt <= AllAttempts; attempt++)
        {
            await handler.Entered.WaitAsync(Ct);
            time.Advance(ResilientClient.NoDelaySettings.AttemptTimeout);
        }

        var result = await upload;

        // Assert
        Assert.Equal(BatchUploadOutcome.Retryable, result.Outcome);
        Assert.Equal(FailureMessages.Timeout, result.Error);
        Assert.Equal(AllAttempts, handler.Attempts);
    }

    [Fact]
    public async Task UploadBatchAsync_CallerCancelsDuringAttempt_ThrowsOperationCanceledAndDoesNotRetry()
    {
        // Arrange
        var time = new FakeTimeProvider(Start);
        var handler = ScriptedHttpHandler.Hanging();
        await using var client = new ResilientClient(handler, ResilientClient.NoDelaySettings, time);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        // Act
        var upload = client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.Pickup)], caller.Token);
        await handler.Entered.WaitAsync(Ct);
        await caller.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task UploadBatchAsync_Http409ProblemDetails_RefusedWithFriendlyServerDetail()
    {
        // Arrange
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Problem(HttpStatusCode.Conflict, SampleJson.ConflictProblem),
            ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.InTransit)], Ct);

        // Assert
        Assert.Equal(BatchUploadOutcome.Refused, result.Outcome);
        Assert.Equal("Cannot move shipment APX-20000005 from Delivered to InTransit.", result.Error);
        Assert.Equal(1, client.Handler.Attempts);
    }

    [Fact]
    public async Task UploadBatchAsync_Http400ValidationProblemDetails_RefusedWithFieldErrorsAsFriendlyMessage()
    {
        // Arrange
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Problem(HttpStatusCode.BadRequest, SampleJson.ValidationProblem),
            ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Uploader.UploadBatchAsync(TrackingNumber, [Scan(0, ScanType.Delivered)], Ct);

        // Assert
        Assert.Equal(BatchUploadOutcome.Refused, result.Outcome);
        Assert.Equal(
            "Please check: The Location field is required. A Delivered scan needs the recipient's signature name.",
            result.Error);
        Assert.Equal(1, client.Handler.Attempts);
    }

    [Fact]
    public async Task UploadAsync_MoqHandler_SendsOnePostWithBearerTokenAndTheScanAsJson()
    {
        // Arrange
        const string answer = """{ "accepted": 1, "duplicatesSkipped": 0, "rejected": [] }""";
        var scan = Scan(0, ScanType.Pickup);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => FakeHttpMessageHandler.Response(HttpStatusCode.OK, answer, SampleJson.Json));
        await using var client = new ResilientClient(handler.Object, ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Uploader.UploadAsync(scan, Ct);

        // Assert
        Assert.Equal(new UploadResult(true, null), result);
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request =>
                request.Method == HttpMethod.Post
                && request.RequestUri!.AbsoluteUri == "https://apexpost.test/api/v2/shipments/APX-20000005/scans"
                && request.Headers.Authorization!.ToString() == $"Bearer {ResilientClient.Token}"
                && request.Content!.Headers.ContentType!.MediaType == "application/json"
                && ((ScanUploadRequest)((JsonContent)request.Content).Value!).Scans.Count == 1
                && ((ScanUploadRequest)((JsonContent)request.Content).Value!).Scans[0].ScanId == scan.Id
                && ((ScanUploadRequest)((JsonContent)request.Content).Value!).Scans[0].CourierId == "CR-101"),
            ItExpr.IsAny<CancellationToken>());
    }

    private static ShipmentScan Scan(int minutes, ScanType type) => ShipmentScan.Restore(
        ScanIds.New(),
        TrackingNumber,
        type,
        "CR-101",
        "Central Hub",
        Start.AddMinutes(minutes),
        null,
        SyncStatus.Pending);
}
