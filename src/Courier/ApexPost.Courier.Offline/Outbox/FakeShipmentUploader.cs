using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.Offline.Outbox;

/// <summary>
/// Stands in for the server while the device works offline. Each upload waits 100–300 ms like a network call,
/// always fails for <see cref="DemoFailureTrackingNumber"/> and otherwise fails at the optional random rate.
/// Part D replaces it with the HTTP uploader behind the same <see cref="IShipmentUploader"/> interface.
/// </summary>
public sealed class FakeShipmentUploader : IShipmentUploader
{
    /// <summary>The tracking number whose scans are always rejected, to demonstrate a failed upload.</summary>
    public const string DemoFailureTrackingNumber = "APX-99999999";

    /// <summary>The shortest simulated network delay.</summary>
    public static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest simulated network delay.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(300);

    private readonly double _failureRate;
    private readonly Random _random;
    private readonly Lock _randomLock = new();

    /// <summary>Creates the fake uploader.</summary>
    /// <param name="failureRate">Share of other uploads that fail at random, from 0 (never) to 1 (always).</param>
    /// <param name="seed">Seed for the random delay and failures, so a run can be repeated exactly.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="failureRate"/> is outside 0..1.</exception>
    public FakeShipmentUploader(double failureRate = 0, int? seed = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(failureRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(failureRate, 1);
        _failureRate = failureRate;
        _random = seed is null ? new Random() : new Random(seed.Value);
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled during the delay.</exception>
    public async Task<UploadResult> UploadAsync(ShipmentScan scan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scan);
        var (delay, randomFailure) = NextAttempt();
        await Task.Delay(delay, ct).ConfigureAwait(false);

        if (string.Equals(scan.TrackingNumber, DemoFailureTrackingNumber, StringComparison.Ordinal))
        {
            return new UploadResult(false, $"Demo failure: {DemoFailureTrackingNumber} is always rejected.");
        }

        return randomFailure
            ? new UploadResult(false, "Simulated network failure.")
            : new UploadResult(true, null);
    }

    // Random is not thread-safe, and the outbox may share one uploader between callers.
    private (TimeSpan Delay, bool Fails) NextAttempt()
    {
        lock (_randomLock)
        {
            var delayMs = _random.Next((int)MinDelay.TotalMilliseconds, (int)MaxDelay.TotalMilliseconds + 1);
            return (TimeSpan.FromMilliseconds(delayMs), _random.NextDouble() < _failureRate);
        }
    }
}
