using System.Net;
using ApexPost.Courier.ApiClient.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ApexPost.Courier.ApiClient.Tests.Resilience;

/// <summary>The two predicates behind the retry: which outcomes are transient, which requests may repeat.</summary>
public sealed class RetryRuleTests
{
    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("OPTIONS", true)]
    [InlineData("PUT", true)]
    [InlineData("DELETE", true)]
    [InlineData("POST", false)]
    [InlineData("PATCH", false)]
    public void Only_idempotent_methods_are_retry_safe_without_a_mark(string method, bool expected)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "api/v2/shipments");

        Assert.Equal(expected, IdempotentRequest.IsRetrySafe(request));
    }

    [Fact]
    public void A_marked_post_is_retry_safe()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v2/shipments/APX-20000005/scans");

        IdempotentRequest.Mark(request);

        Assert.True(IdempotentRequest.IsRetrySafe(request));
    }

    [Fact]
    public void A_missing_request_is_not_retry_safe() => Assert.False(IdempotentRequest.IsRetrySafe(null));

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Conflict, false)]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    public void Only_408_and_5xx_answers_are_transient(HttpStatusCode status, bool expected)
    {
        using var response = new HttpResponseMessage(status);

        Assert.Equal(expected, TransientFailure.Is(Outcome.FromResult(response)));
    }

    [Fact]
    public void Network_errors_and_attempt_timeouts_are_transient_but_other_exceptions_are_not()
    {
        Assert.True(TransientFailure.Is(Outcome.FromException<HttpResponseMessage>(new HttpRequestException())));
        Assert.True(TransientFailure.Is(Outcome.FromException<HttpResponseMessage>(new TimeoutRejectedException())));
        Assert.False(TransientFailure.Is(Outcome.FromException<HttpResponseMessage>(new OperationCanceledException())));
        Assert.False(TransientFailure.Is(Outcome.FromException<HttpResponseMessage>(new BrokenCircuitException())));
    }

    [Fact]
    public void The_default_settings_are_the_documented_values()
    {
        var settings = new ResilienceSettings();

        Assert.Equal(TimeSpan.FromSeconds(30), settings.TotalTimeout);
        Assert.Equal(TimeSpan.FromSeconds(8), settings.AttemptTimeout);
        Assert.Equal(3, settings.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(1), settings.RetryBaseDelay);
        Assert.Equal(0.5, settings.FailureRatio);
        Assert.Equal(4, settings.MinimumThroughput);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.SamplingDuration);
        Assert.Equal(TimeSpan.FromSeconds(15), settings.BreakDuration);
    }
}
