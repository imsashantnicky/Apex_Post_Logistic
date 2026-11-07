using System.Net;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Tests.Support;
using Polly.CircuitBreaker;

namespace ApexPost.Courier.ApiClient.Tests.Resilience;

/// <summary>Circuit breaker: opens after repeated failures, blocks calls while open, closes after a good trial.</summary>
public sealed class CircuitBreakerTests
{
    private const string TrackingNumber = "APX-20000005";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_circuit_starts_closed()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(SampleJson.Shipment, HttpStatusCode.OK));

        Assert.Equal(CircuitState.Closed, client.Circuit.State);
        Assert.False(client.Circuit.IsServerUnavailable);
    }

    [Fact]
    public async Task Repeated_503s_open_the_circuit_and_raise_the_state_change()
    {
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Statuses(SampleJson.Shipment, HttpStatusCode.ServiceUnavailable));
        var changes = new List<CircuitState>();
        client.Circuit.StateChanged += (_, state) => changes.Add(state);

        await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.Equal(CircuitState.Open, client.Circuit.State);
        Assert.True(client.Circuit.IsServerUnavailable);
        Assert.Equal([CircuitState.Open], changes);
    }

    [Fact]
    public async Task While_open_no_request_is_sent_and_the_failure_says_working_offline()
    {
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Statuses(SampleJson.Shipment, HttpStatusCode.ServiceUnavailable));
        await client.Api.GetShipmentAsync(TrackingNumber, Ct);
        var attemptsBefore = client.Handler.Attempts;

        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.ServerUnavailable, result.Failure.Kind);
        Assert.StartsWith("Server unavailable – working offline", result.Failure.Message, StringComparison.Ordinal);
        Assert.Equal(attemptsBefore, client.Handler.Attempts);
    }

    [Fact]
    public async Task Client_errors_never_open_the_circuit()
    {
        await using var client = new ResilientClient(
            ScriptedHttpHandler.Statuses(SampleJson.Shipment, HttpStatusCode.NotFound));

        for (var call = 0; call < 6; call++)
        {
            await client.Api.GetShipmentAsync(TrackingNumber, Ct);
        }

        Assert.Equal(CircuitState.Closed, client.Circuit.State);
        Assert.Equal(6, client.Handler.Attempts);
    }

    [Fact]
    public async Task After_the_break_a_good_trial_call_closes_the_circuit()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(
            SampleJson.Shipment,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK));
        var changes = new List<CircuitState>();
        client.Circuit.StateChanged += (_, state) => changes.Add(state);
        await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        await Task.Delay(ResilientClient.FastSettings.BreakDuration + TimeSpan.FromMilliseconds(200), Ct);
        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(CircuitState.Closed, client.Circuit.State);
        Assert.Equal([CircuitState.Open, CircuitState.HalfOpen, CircuitState.Closed], changes);
    }
}
