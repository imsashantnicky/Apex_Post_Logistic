using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

public sealed class BranchServiceTests
{
    [Fact]
    public async Task Administrator_adds_and_removes_counters()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var branches = harness.BranchAdmin();
        var before = await branches.GetAsync(harness.Branch.Code, Ct);

        await branches.AddCounterAsync(before.Code, before.Version, Ct);
        var added = await branches.GetAsync(harness.Branch.Code, Ct);
        await branches.RemoveCounterAsync(before.Code, added.Counters[1].Id, added.Version, Ct);

        Assert.Equal(2, added.Counters.Count);
        Assert.Single((await branches.GetAsync(harness.Branch.Code, Ct)).Counters);
    }

    [Fact]
    public async Task Counter_with_an_open_basket_stays()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var branch = await harness.BranchAdmin().GetAsync(harness.Branch.Code, Ct);
        await harness.BranchAdmin().AddCounterAsync(branch.Code, branch.Version, Ct);
        harness.As(harness.Clerk);
        await harness.OpenAsync();
        harness.As(harness.Admin);
        var current = await harness.BranchAdmin().GetAsync(branch.Code, Ct);

        await Assert.ThrowsAsync<DomainException>(() =>
            harness.BranchAdmin().RemoveCounterAsync(branch.Code, current.Counters[0].Id, current.Version, Ct));

        Assert.Equal(2, (await harness.BranchAdmin().GetAsync(branch.Code, Ct)).Counters.Count);
    }

    [Fact]
    public async Task Counter_with_only_finished_baskets_can_go()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var branch = await harness.BranchAdmin().GetAsync(harness.Branch.Code, Ct);
        await harness.BranchAdmin().AddCounterAsync(branch.Code, branch.Version, Ct);
        harness.As(harness.Clerk);
        var paid = await harness.OpenAsync();
        await harness.Checkout().AddRetailAsync(paid, ServiceHarness.StandardSku);
        await harness.Checkout().PayAsync(paid, PaymentMethod.Card, Ct);
        harness.As(harness.Admin);
        var current = await harness.BranchAdmin().GetAsync(branch.Code, Ct);

        await harness.BranchAdmin().RemoveCounterAsync(branch.Code, current.Counters[0].Id, current.Version, Ct);

        Assert.Equal(TransactionStatus.Paid, (await harness.TransactionAsync(paid)).Status);
    }

    [Theory]
    [InlineData(StaffRole.CounterClerk)]
    [InlineData(StaffRole.BranchManager)]
    [InlineData(StaffRole.Auditor)]
    public async Task Only_administrators_change_branches(StaffRole role)
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);
        harness.User.Role = role;
        var branches = harness.BranchAdmin();
        var code = harness.Branch.Code;

        await Assert.ThrowsAsync<ForbiddenException>(() => branches.CloseAsync(code, 1, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => branches.ReopenAsync(code, 1, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => branches.AddCounterAsync(code, 1, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            branches.RemoveCounterAsync(code, harness.Branch.Counters[0].Id, 1, Ct));
    }

    [Fact]
    public async Task Stale_counter_change_is_a_conflict()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var branches = harness.BranchAdmin();
        var seen = await branches.GetAsync(harness.Branch.Code, Ct);
        await branches.AddCounterAsync(seen.Code, seen.Version, Ct);

        await Assert.ThrowsAsync<ConcurrencyException>(() => branches.AddCounterAsync(seen.Code, seen.Version, Ct));

        Assert.Equal(2, (await branches.GetAsync(seen.Code, Ct)).Counters.Count);
    }

    [Fact]
    public async Task Closing_stops_work_on_baskets_already_open_but_a_manager_can_still_void_them()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();
        var checkout = harness.Checkout();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku);
        await CloseHomeBranchAsync(harness);
        harness.As(harness.Clerk);

        await Assert.ThrowsAsync<DomainException>(() => checkout.PayAsync(id, PaymentMethod.Card, Ct));
        await Assert.ThrowsAsync<DomainException>(() => checkout.AddRetailAsync(id, ServiceHarness.StandardSku));
        await Assert.ThrowsAsync<DomainException>(() => checkout.AddParcelAsync(id, 1m));
        await Assert.ThrowsAsync<DomainException>(() => checkout.AddMoneyOrderAsync(id, 50m));
        await Assert.ThrowsAsync<DomainException>(() => checkout.VerifyAsync(id));
        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));

        harness.As(harness.Manager);
        await harness.Reversal().VoidAsync(id, new ReversalCommand { Reason = "Branch closed." }, Ct);

        Assert.Equal(TransactionStatus.Voided, (await harness.TransactionAsync(id)).Status);
    }

    [Fact]
    public async Task Paid_sale_of_a_closed_branch_can_still_be_refunded()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();
        await harness.Checkout().AddRetailAsync(id, ServiceHarness.StandardSku);
        await harness.Checkout().PayAsync(id, PaymentMethod.Card, Ct);
        await CloseHomeBranchAsync(harness);
        harness.As(harness.Manager);

        await harness.Reversal().RefundAsync(id, new ReversalCommand { Reason = "Damaged." }, Ct);

        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    private static async Task CloseHomeBranchAsync(ServiceHarness harness)
    {
        harness.As(harness.Admin);
        var branch = await harness.BranchAdmin().GetAsync(harness.Branch.Code, Ct);
        await harness.BranchAdmin().CloseAsync(branch.Code, branch.Version, Ct);
    }
}
