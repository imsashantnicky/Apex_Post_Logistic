using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace ApexPost.Courier.ApiClient.Resilience;

/// <summary>
/// Adds the resilience pipeline to the typed <see cref="ApexApiClient"/>. Order, outermost first:
/// total timeout → retry → circuit breaker → attempt timeout. The breaker sits inside the retry, so every attempt
/// is counted and an open circuit stops the remaining retries at once.
/// </summary>
public static class ApexResilience
{
    /// <summary>Name of the pipeline (shown in Polly's own telemetry).</summary>
    public const string PipelineName = "apex-api";

    /// <summary>Logger category of the retry and circuit messages.</summary>
    public const string LogCategory = "ApexPost.Courier.ApiClient.Resilience";

    /// <summary>
    /// Registers <see cref="ICircuitStateProvider"/> and adds the pipeline as a handler of <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The typed client registration.</param>
    /// <param name="settings">Pipeline values; <see langword="null"/> uses the production defaults.</param>
    public static IHttpClientBuilder AddApexResilience(
        this IHttpClientBuilder builder,
        ResilienceSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var values = settings ?? new ResilienceSettings();

        builder.Services.TryAddSingleton<CircuitStateTracker>();
        builder.Services.TryAddSingleton<ICircuitStateProvider>(
            provider => provider.GetRequiredService<CircuitStateTracker>());

        builder.AddResilienceHandler(PipelineName, (pipeline, context) =>
        {
            var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(LogCategory);
            var tracker = context.ServiceProvider.GetRequiredService<CircuitStateTracker>();
            pipeline
                .AddTimeout(values.TotalTimeout)
                .AddRetry(CreateRetry(values, logger))
                .AddCircuitBreaker(CreateCircuitBreaker(values, tracker, logger))
                .AddTimeout(values.AttemptTimeout);
        });

        return builder;
    }

    private static HttpRetryStrategyOptions CreateRetry(ResilienceSettings values, ILogger logger) => new()
    {
        MaxRetryAttempts = values.MaxRetryAttempts,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = values.RetryBaseDelay,
        MaxDelay = values.RetryMaxDelay,

        // Transient failure AND a request that is safe to repeat; a 4xx or a plain POST/PATCH is never retried.
        ShouldHandle = args => ValueTask.FromResult(
            TransientFailure.Is(args.Outcome) && IdempotentRequest.IsRetrySafe(args.Context.GetRequestMessage())),
        OnRetry = args =>
        {
            var request = args.Context.GetRequestMessage();
            ResilienceLog.Retrying(
                logger,
                args.AttemptNumber + 1,
                values.MaxRetryAttempts,
                request?.Method.Method ?? "?",
                request?.RequestUri?.AbsolutePath ?? "?",
                (long)args.RetryDelay.TotalMilliseconds,
                TransientFailure.Describe(args.Outcome));
            return ValueTask.CompletedTask;
        },
    };

    private static HttpCircuitBreakerStrategyOptions CreateCircuitBreaker(
        ResilienceSettings values,
        CircuitStateTracker tracker,
        ILogger logger) => new()
    {
        FailureRatio = values.FailureRatio,
        MinimumThroughput = values.MinimumThroughput,
        SamplingDuration = values.SamplingDuration,
        BreakDuration = values.BreakDuration,

        // Every method counts here: the breaker measures server health, not whether a request may be repeated.
        ShouldHandle = args => ValueTask.FromResult(TransientFailure.Is(args.Outcome)),
        OnOpened = args =>
        {
            var reason = TransientFailure.Describe(args.Outcome);
            ResilienceLog.CircuitOpened(logger, args.BreakDuration.TotalSeconds, reason);
            tracker.Update(CircuitState.Open);
            return ValueTask.CompletedTask;
        },
        OnHalfOpened = _ =>
        {
            ResilienceLog.CircuitHalfOpened(logger);
            tracker.Update(CircuitState.HalfOpen);
            return ValueTask.CompletedTask;
        },
        OnClosed = _ =>
        {
            ResilienceLog.CircuitClosed(logger);
            tracker.Update(CircuitState.Closed);
            return ValueTask.CompletedTask;
        },
    };
}
