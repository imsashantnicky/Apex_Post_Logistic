using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Tests.Support;
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Courier.ApiClient.Tests.Resilience;

/// <summary>
/// Attempt timeout (retried) and total timeout (returned as a Timeout failure). Polly runs on a
/// <see cref="FakeTimeProvider"/>: each test waits until an attempt reaches the handler and then advances the clock,
/// so the result never depends on how fast the machine is.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TimeoutTests
{
    private const string TrackingNumber = "APX-20000005";

    private static readonly DateTimeOffset Start = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private static readonly ResilienceSettings Settings = new()
    {
        TotalTimeout = TimeSpan.FromMilliseconds(300),
        AttemptTimeout = TimeSpan.FromMilliseconds(200),
        RetryBaseDelay = TimeSpan.Zero,
        RetryMaxDelay = TimeSpan.FromMilliseconds(1),
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AttemptTimeout_FirstAttemptHangs_IsCutAndRetriedToSuccess()
    {
        // Arrange
        var time = new FakeTimeProvider(Start);
        var handler = ScriptedHttpHandler.HangingFirst(1, SampleJson.Shipment);
        await using var client = new ResilientClient(handler, Settings, time);

        // Act
        var call = client.Api.GetShipmentAsync(TrackingNumber, Ct);
        await handler.Entered.WaitAsync(Ct);
        time.Advance(Settings.AttemptTimeout);
        var result = await call;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.Attempts);
        Assert.Contains(client.ResilienceLogs, log => log.Message.EndsWith("TimeoutRejectedException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TotalTimeout_EveryAttemptHangs_EndsTheCallWithTimeoutFailure()
    {
        // Arrange
        var time = new FakeTimeProvider(Start);
        var handler = ScriptedHttpHandler.HangingFirst(int.MaxValue, SampleJson.Shipment);
        await using var client = new ResilientClient(handler, Settings, time);

        // Act
        var call = client.Api.GetShipmentAsync(TrackingNumber, Ct);
        await handler.Entered.WaitAsync(Ct);
        time.Advance(Settings.AttemptTimeout);
        await handler.Entered.WaitAsync(Ct);
        time.Advance(Settings.TotalTimeout - Settings.AttemptTimeout);
        var result = await call;

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.Timeout, result.Failure.Kind);
        Assert.Equal(FailureMessages.Timeout, result.Failure.Message);
        Assert.Equal(2, handler.Attempts);
    }
}
