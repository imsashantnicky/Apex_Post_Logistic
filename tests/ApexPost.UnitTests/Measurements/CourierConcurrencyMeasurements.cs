using System.Globalization;
using ApexPost.Application.Settings;
using ApexPost.Infrastructure.Shipping;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Options;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Measurements;

[Trait("Category", "Measurement")]
[Collection(nameof(SerialMeasurements))]
public sealed class CourierConcurrencyMeasurements(ITestOutputHelper output)
{
    private const int Repeats = 7;
    private const int Lookups = 30;
    private const int TimeoutMs = 60_000;

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public async Task Parallel_lookups_with_a_semaphore_limit_of(int limit)
    {
        using var courier = new SimulatedCourierService(
            new FixedClock(),
            Options.Create(new CourierSettings { MaxConcurrentRequests = limit, TimeoutMilliseconds = TimeoutMs }));

        var time = await MeasuredTime.OfAsync(Repeats, () => Task.WhenAll(
            Enumerable.Range(0, Lookups).Select(_ => courier.GetStatusAsync("APX123456", Ct))));

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Limit {limit}, {Lookups} lookups: {time} = {Lookups * 1000 / time.MedianMs:F0} lookups/s"));
        Assert.True(time.MedianMs > 0);
    }
}
