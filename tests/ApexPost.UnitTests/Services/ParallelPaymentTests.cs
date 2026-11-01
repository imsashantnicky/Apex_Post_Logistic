using ApexPost.Application.Services;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Transactions;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

/// <summary>The stock gate serialises stock changes per branch, so parallel sales never oversell.</summary>
public sealed class ParallelPaymentTests
{
    [Fact]
    public async Task Two_counters_selling_the_last_items_at_once_sell_them_only_once()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var first = await harness.OpenAsync();
        var second = await harness.OpenAsync();
        await checkout.AddRetailAsync(first, ServiceHarness.StandardSku, 6);
        await checkout.AddRetailAsync(second, ServiceHarness.StandardSku, 6);

        var results = await Task.WhenAll(
            Outcome(harness.Checkout().PayAsync(first, PaymentMethod.Card, Ct)),
            Outcome(harness.Checkout().PayAsync(second, PaymentMethod.Card, Ct)));

        Assert.Single(results, error => error is null);
        Assert.Single(results, error => error is InsufficientStockException);
        Assert.Equal(4, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Many_parallel_payments_never_take_stock_below_zero()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        List<Guid> baskets = [];
        for (var i = 0; i < 15; i++)
        {
            var id = await harness.OpenAsync();
            await checkout.AddRetailAsync(id, ServiceHarness.StandardSku);
            baskets.Add(id);
        }

        var results = await Task.WhenAll(baskets.Select(id =>
            Outcome(harness.Checkout().PayAsync(id, PaymentMethod.Card, Ct))));

        Assert.Equal(ServiceHarness.InitialStock, results.Count(error => error is null));
        Assert.All(results.OfType<Exception>(), error => Assert.IsType<InsufficientStockException>(error));
        Assert.Equal(0, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Paying_the_same_basket_twice_at_once_takes_the_stock_once()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku, 2);

        var results = await Task.WhenAll(
            Outcome(harness.Checkout().PayAsync(id, PaymentMethod.Card, Ct)),
            Outcome(harness.Checkout().PayAsync(id, PaymentMethod.Cash, Ct)));

        Assert.Single(results, error => error is null);
        Assert.Single(results, error => error is DomainException);
        Assert.Equal(8, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Gate_runs_one_change_per_branch_at_a_time()
    {
        using var gate = new StockGate();
        var branch = new BranchCode(TestData.HomeBranch);
        var running = 0;
        var maximum = 0;

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => gate.RunAsync(
            branch,
            async () =>
            {
                var now = Interlocked.Increment(ref running);
                InterlockedMax(ref maximum, now);
                await Task.Delay(5, Ct);
                Interlocked.Decrement(ref running);
            },
            Ct)));

        Assert.Equal(1, maximum);
    }

    [Fact]
    public async Task Gate_does_not_make_other_branches_wait()
    {
        using var gate = new StockGate();
        var release = new TaskCompletionSource();
        var held = gate.RunAsync(new BranchCode(TestData.HomeBranch), () => release.Task, Ct);

        var other = await gate.RunAsync(new BranchCode(TestData.OtherBranch), () => Task.FromResult(42), Ct);

        Assert.Equal(42, other);
        Assert.False(held.IsCompleted);
        release.SetResult();
        await held;
    }

    [Fact]
    public async Task Waiting_for_the_gate_can_be_cancelled_and_a_failure_releases_it()
    {
        using var gate = new StockGate();
        var branch = new BranchCode(TestData.HomeBranch);
        var release = new TaskCompletionSource();
        var held = gate.RunAsync(branch, () => release.Task, Ct);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gate.RunAsync(branch, () => Task.CompletedTask, cancelled.Token));
        release.SetResult();
        await held;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.RunAsync(branch, () => Task.FromException(new InvalidOperationException()), Ct));

        Assert.Equal(1, await gate.RunAsync(branch, () => Task.FromResult(1), Ct));
    }

    private static async Task<Exception?> Outcome(Task payment)
    {
        try
        {
            await payment;
            return null;
        }
        catch (DomainException error)
        {
            return error;
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        do
        {
            current = target;
        }
        while (value > current && Interlocked.CompareExchange(ref target, value, current) != current);
    }
}
