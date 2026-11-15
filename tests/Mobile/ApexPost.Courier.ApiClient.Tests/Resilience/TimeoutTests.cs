using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Tests.Support;

namespace ApexPost.Courier.ApiClient.Tests.Resilience;

/// <summary>Attempt timeout (retried) and total timeout (returned as a Timeout failure).</summary>
public sealed class TimeoutTests
{
    private const string TrackingNumber = "APX-20000005";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_hanging_attempt_is_cut_by_the_attempt_timeout_and_retried()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.HangingFirst(1, SampleJson.Shipment));

        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, client.Handler.Attempts);
        Assert.Contains(client.ResilienceLogs, log => log.Message.EndsWith("TimeoutRejectedException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_total_timeout_ends_the_call_with_a_timeout_failure()
    {
        var settings = new ResilienceSettings
        {
            TotalTimeout = TimeSpan.FromMilliseconds(300),
            AttemptTimeout = TimeSpan.FromMilliseconds(200),
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        };
        await using var client = new ResilientClient(ScriptedHttpHandler.HangingFirst(int.MaxValue, SampleJson.Shipment), settings);

        var result = await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.Timeout, result.Failure.Kind);
        Assert.Equal(FailureMessages.Timeout, result.Failure.Message);
        Assert.Equal(2, client.Handler.Attempts);
    }
}
