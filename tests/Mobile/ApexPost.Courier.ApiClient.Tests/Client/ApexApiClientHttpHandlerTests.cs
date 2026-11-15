using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Tests.Support;
using ApexPost.Courier.Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Protected;

namespace ApexPost.Courier.ApiClient.Tests.Client;

/// <summary>
/// One test per HTTP case for <see cref="ApexApiClient"/>, through the real registration (auth handler + resilience
/// pipeline) on a fake or Moq <see cref="HttpMessageHandler"/> behind the <see cref="NetworkGuardHandler"/>.
/// Retries have zero delay and timeouts run on a <see cref="FakeTimeProvider"/>, so no case waits on the wall clock.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ApexApiClientHttpHandlerTests
{
    private const string TrackingNumber = "APX-20000005";

    private static readonly DateTimeOffset Start = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private static int AllAttempts => 1 + ResilientClient.NoDelaySettings.MaxRetryAttempts;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetShipmentAsync_Http200WithV2Json_ReturnsShipmentWithStringEnumsAndNestedRecipient()
    {
        // Arrange
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Statuses(SampleJson.Shipment, HttpStatusCode.OK), ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TrackingNumber, result.Value.TrackingNumber);
        Assert.Equal(ShipmentStatus.InTransit, result.Value.Status);
        Assert.Equal(ScanType.InTransit, result.Value.LatestScan?.ScanType);
        Assert.Equal("Mia Example", result.Value.Recipient.Name);
        Assert.Equal(2.5m, result.Value.WeightKg);
        Assert.Equal(1, client.Handler.Attempts);
    }

    [Fact]
    public async Task GetShipmentAsync_Http404ProblemDetails_ReturnsNotFoundWithServerDetailAndNoRetry()
    {
        // Arrange
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Problem(HttpStatusCode.NotFound, SampleJson.NotFoundProblem),
            ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Api.GetShipmentAsync("APX-00000000", Ct);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.NotFound, result.Failure.Kind);
        Assert.Equal("Shipment APX-00000000 was not found.", result.Failure.Message);
        Assert.Equal(404, result.Failure.StatusCode);
        Assert.Equal(1, client.Handler.Attempts);
    }

    [Fact]
    public async Task GetShipmentAsync_Http500OnEveryAttempt_RetriesPerPolicyThenReturnsServerFailure()
    {
        // Arrange
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Problem(HttpStatusCode.InternalServerError, SampleJson.ServerProblem),
            ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.Server, result.Failure.Kind);
        Assert.Equal(500, result.Failure.StatusCode);
        Assert.Equal("The ApexPost server had a problem. Try again later.", result.Failure.Message);
        Assert.DoesNotContain("InvalidOperationException", result.Failure.Message, StringComparison.Ordinal);
        Assert.Equal(AllAttempts, client.Handler.Attempts);
    }

    [Fact]
    public async Task GetShipmentAsync_EveryAttemptTimesOut_RetriesPerPolicyThenReturnsTimeoutFailure()
    {
        // Arrange
        var time = new FakeTimeProvider(Start);
        var handler = ScriptedHttpHandler.Hanging();
        await using var client = new ResilientClient(handler, ResilientClient.NoDelaySettings, time);

        // Act
        var call = client.Api.GetShipmentAsync(TrackingNumber, Ct);
        for (var attempt = 1; attempt <= AllAttempts; attempt++)
        {
            await handler.Entered.WaitAsync(Ct);
            time.Advance(ResilientClient.NoDelaySettings.AttemptTimeout);
        }

        var result = await call;

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.Timeout, result.Failure.Kind);
        Assert.Equal(FailureMessages.Timeout, result.Failure.Message);
        Assert.Null(result.Failure.StatusCode);
        Assert.Equal(AllAttempts, handler.Attempts);
    }

    [Fact]
    public async Task GetShipmentAsync_CallerCancelsDuringAttempt_ThrowsOperationCanceledAndDoesNotRetry()
    {
        // Arrange
        var time = new FakeTimeProvider(Start);
        var handler = ScriptedHttpHandler.Hanging();
        await using var client = new ResilientClient(handler, ResilientClient.NoDelaySettings, time);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        // Act
        var call = client.Api.GetShipmentAsync(TrackingNumber, caller.Token);
        await handler.Entered.WaitAsync(Ct);
        await caller.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task ChangeStatusAsync_Http409ProblemDetails_ReturnsConflictWithFriendlyDetailAndTraceId()
    {
        // Arrange
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Problem(HttpStatusCode.Conflict, SampleJson.ConflictProblem),
            ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Api.ChangeStatusAsync(TrackingNumber, ShipmentStatus.InTransit, Ct);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.Conflict, result.Failure.Kind);
        Assert.Equal("Cannot move shipment APX-20000005 from Delivered to InTransit.", result.Failure.Message);
        Assert.Equal("00-abc-def-04", result.Failure.TraceId);
        Assert.Empty(result.Failure.FieldErrors);
        Assert.Equal(1, client.Handler.Attempts);
    }

    [Fact]
    public async Task UploadScansAsync_Http400ValidationProblemDetails_ReturnsFieldErrorsAsFriendlyMessage()
    {
        // Arrange
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Problem(HttpStatusCode.BadRequest, SampleJson.ValidationProblem),
            ResilientClient.NoDelaySettings);

        // Act
        var result = await client.Api.UploadScansAsync(TrackingNumber, new ScanUploadRequest([Scan()]), Ct);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.Validation, result.Failure.Kind);
        Assert.Equal(
            "Please check: The Location field is required. A Delivered scan needs the recipient's signature name.",
            result.Failure.Message);
        Assert.Equal(["The Location field is required."], result.Failure.FieldErrors["Scans[0].Location"]);
        Assert.Equal("00-abc-def-02", result.Failure.TraceId);
        Assert.Equal(1, client.Handler.Attempts);
    }

    [Fact]
    public async Task UploadScansAsync_MoqHandler_SendsOnePostWithBearerTokenAndJsonBody()
    {
        // Arrange
        const string answer = """{ "accepted": 1, "duplicatesSkipped": 0, "rejected": [] }""";
        var scan = Scan();
        string? sentJson = null;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
            {
                sentJson = await request.Content!.ReadAsStringAsync(ct);
                return FakeHttpMessageHandler.Response(HttpStatusCode.OK, answer, SampleJson.Json);
            });
        var auth = new AuthHeaderHandler(new InMemoryTokenStore("moq-token"))
        {
            InnerHandler = new NetworkGuardHandler(handler.Object),
        };
        var api = new ApexApiClient(new HttpClient(auth) { BaseAddress = ClientFactory.BaseAddress });

        // Act
        var result = await api.UploadScansAsync(TrackingNumber, new ScanUploadRequest([scan]), Ct);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Accepted);
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request =>
                request.Method == HttpMethod.Post
                && request.RequestUri!.Host == NetworkGuardHandler.FakeHost
                && request.RequestUri.AbsolutePath == "/api/v2/shipments/APX-20000005/scans"
                && request.Headers.Authorization!.Scheme == "Bearer"
                && request.Headers.Authorization.Parameter == "moq-token"
                && request.Content is JsonContent
                && ((JsonContent)request.Content).Value is ScanUploadRequest
                && ((ScanUploadRequest)((JsonContent)request.Content).Value!).Scans[0].ScanId == scan.ScanId),
            ItExpr.IsAny<CancellationToken>());
        using var body = JsonDocument.Parse(sentJson!);
        var item = body.RootElement.GetProperty("scans")[0];
        Assert.Equal(scan.ScanId, item.GetProperty("scanId").GetString());
        Assert.Equal("Delivered", item.GetProperty("scanType").GetString());
        Assert.Equal("CR-101", item.GetProperty("courierId").GetString());
        Assert.Equal("2026-11-05T10:00:00Z", item.GetProperty("scannedAtUtc").GetString());
    }

    private static ScanItem Scan() => new(
        "scan::0f8fad5b-d9cb-469f-a165-70867728950e",
        TrackingNumber,
        ScanType.Delivered,
        "CR-101",
        "Northgate",
        new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc),
        null,
        "Mia Example");
}
