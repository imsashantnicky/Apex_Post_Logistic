using System.Diagnostics;
using System.Globalization;

namespace ApexPost.UnitTests.Measurements;

/// <summary>Wall-clock time of repeated runs. The first run warms up JIT and caches and is not counted.</summary>
internal sealed record MeasuredTime(double MedianMs, double MinMs, double MaxMs)
{
    public static async Task<MeasuredTime> OfAsync(int repeats, Func<Task> action)
    {
        await action();
        var samples = new double[repeats];
        for (var repeat = 0; repeat < repeats; repeat++)
        {
            var start = Stopwatch.GetTimestamp();
            await action();
            samples[repeat] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        Array.Sort(samples);
        return new MeasuredTime(samples[repeats / 2], samples[0], samples[^1]);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"median {MedianMs:F2} ms (min {MinMs:F2}, max {MaxMs:F2})");
}
