namespace ApexPost.Courier.ApiClient.Resilience;

/// <summary>
/// Values of the resilience pipeline around <see cref="ApexApiClient"/>. The defaults are the production values
/// explained in <c>RESILIENCE.md</c>; tests pass shorter times so they run in milliseconds.
/// </summary>
public sealed class ResilienceSettings
{
    /// <summary>Upper limit for one call including every retry and wait (outermost strategy).</summary>
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Upper limit for a single HTTP attempt (innermost strategy).</summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>Retries after the first attempt, so one call makes at most this number plus one attempts.</summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>Base delay of the exponential backoff (about 1 s, 2 s, 4 s before jitter).</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Longest wait between two attempts, also when the server sends a larger <c>Retry-After</c>.</summary>
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>Share of failed attempts in the sampling window that opens the circuit.</summary>
    public double FailureRatio { get; init; } = 0.5;

    /// <summary>Attempts the sampling window needs before the failure ratio counts.</summary>
    public int MinimumThroughput { get; init; } = 4;

    /// <summary>Length of the window in which attempts are counted.</summary>
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long the circuit stays open before one trial call is let through.</summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(15);
}
