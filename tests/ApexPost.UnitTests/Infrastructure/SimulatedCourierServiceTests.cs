using System.Diagnostics;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Settings;
using ApexPost.Infrastructure.Shipping;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Options;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class SimulatedCourierServiceTests
{
    private const int ShortTimeoutMs = 100;
    private const int LongTimeoutMs = 30_000;

    [Theory]
    [InlineData("APX123456")]
    [InlineData(" apx123456 ")]
    public async Task Known_number_returns_its_status(string number)
    {
        using var courier = Courier(LongTimeoutMs);

        var status = await courier.GetStatusAsync(number, Ct);

        Assert.Equal("APX123456", status.TrackingNumber);
        Assert.Equal(SimulatedCourierService.InTransit, status.Status);
        Assert.Equal(TestData.Now, status.UpdatedAt);
    }

    [Theory]
    [InlineData("SLOW1")]
    [InlineData("slow1")]
    public async Task Slow_courier_is_a_timeout(string number)
    {
        using var courier = Courier(ShortTimeoutMs);

        await Assert.ThrowsAsync<ExternalServiceTimeoutException>(() => courier.GetStatusAsync(number, Ct));
    }

    [Fact]
    public async Task Failing_courier_is_a_failure_not_a_timeout()
    {
        using var courier = Courier(LongTimeoutMs);

        var exception = await Assert.ThrowsAsync<ExternalServiceException>(() => courier.GetStatusAsync("FAIL1", Ct));

        Assert.IsNotType<ExternalServiceTimeoutException>(exception);
    }

    [Fact]
    public async Task Caller_cancel_is_a_cancellation_not_a_timeout()
    {
        using var courier = Courier(LongTimeoutMs);
        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => courier.GetStatusAsync("SLOW1", caller.Token));
    }

    [Fact]
    public async Task Caller_cancel_before_the_call_is_a_cancellation()
    {
        using var courier = Courier(LongTimeoutMs);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            courier.GetStatusAsync("APX123456", new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Waiting_for_a_slot_counts_toward_the_timeout()
    {
        using var courier = Courier(ShortTimeoutMs, maxConcurrent: 1);
        var blocker = courier.GetStatusAsync("SLOW1", Ct);

        await Assert.ThrowsAsync<ExternalServiceTimeoutException>(() => courier.GetStatusAsync("APX123456", Ct));
        await Assert.ThrowsAsync<ExternalServiceTimeoutException>(() => blocker);
    }

    [Theory]
    [InlineData("SLOW1")]
    [InlineData("FAIL1")]
    public async Task A_slot_is_given_back_after_an_error(string number)
    {
        using var courier = Courier(ShortTimeoutMs, maxConcurrent: 1);
        await Assert.ThrowsAnyAsync<ExternalServiceException>(() => courier.GetStatusAsync(number, Ct));

        var status = await courier.GetStatusAsync("APX123456", Ct);

        Assert.Equal(SimulatedCourierService.InTransit, status.Status);
    }

    [Fact]
    public async Task One_slot_runs_the_calls_one_after_the_other()
    {
        const int Calls = 4;
        const int AtLeastMs = 50;
        using var courier = Courier(LongTimeoutMs, maxConcurrent: 1);
        var watch = Stopwatch.StartNew();

        await Task.WhenAll(Enumerable.Range(0, Calls).Select(_ => courier.GetStatusAsync("APX123456", Ct)));

        Assert.True(watch.ElapsedMilliseconds >= AtLeastMs, $"Took only {watch.ElapsedMilliseconds} ms.");
    }

    private static SimulatedCourierService Courier(int timeoutMs, int maxConcurrent = 3) =>
        new(
            new FixedClock(),
            Options.Create(new CourierSettings
            {
                TimeoutMilliseconds = timeoutMs,
                MaxConcurrentRequests = maxConcurrent,
            }));
}
