using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

/// <summary>Rule 5: voids and refunds need the Branch Manager of the branch and a reason; stock comes back.</summary>
public sealed class ReversalServiceTests
{
    private static readonly ReversalCommand Reason = new() { Reason = "Customer changed their mind" };

    [Fact]
    public async Task Voiding_a_paid_transaction_returns_the_stock_and_notifies()
    {
        using var harness = new ServiceHarness();
        var id = await PaidSaleAsync(harness, quantity: 3);
        harness.As(harness.Manager);

        await harness.Reversal().VoidAsync(id, Reason, Ct);

        var details = await harness.Checkout().GetAsync(id, Ct);
        Assert.Equal(TransactionStatus.Voided, details.Status);
        Assert.Equal(Reason.Reason, details.ReversalReason);
        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));
        Assert.Equal(NotificationKind.Void, Assert.Single(harness.Sink.Published).Kind);
    }

    [Fact]
    public async Task Voiding_an_open_transaction_leaves_the_stock_alone()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();
        await harness.Checkout().AddRetailAsync(id, ServiceHarness.StandardSku, 3);
        harness.As(harness.Manager);

        await harness.Reversal().VoidAsync(id, Reason, Ct);

        Assert.Equal(TransactionStatus.Voided, (await harness.TransactionAsync(id)).Status);
        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Refunding_a_paid_transaction_returns_the_stock_and_notifies()
    {
        using var harness = new ServiceHarness();
        var id = await PaidSaleAsync(harness, quantity: 4);
        Assert.Equal(6, await harness.StockOfAsync(ServiceHarness.StandardSku));
        harness.As(harness.Manager);

        await harness.Reversal().RefundAsync(id, Reason, Ct);

        Assert.Equal(TransactionStatus.Refunded, (await harness.TransactionAsync(id)).Status);
        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));
        Assert.Equal(NotificationKind.Refund, Assert.Single(harness.Sink.Published).Kind);
    }

    [Fact]
    public async Task Only_a_paid_transaction_can_be_refunded_and_only_once()
    {
        using var harness = new ServiceHarness();
        var open = await harness.OpenAsync();
        var paid = await PaidSaleAsync(harness, quantity: 1);
        harness.As(harness.Manager);
        var reversal = harness.Reversal();
        await reversal.RefundAsync(paid, Reason, Ct);

        await Assert.ThrowsAsync<DomainException>(() => reversal.RefundAsync(open, Reason, Ct));
        await Assert.ThrowsAsync<DomainException>(() => reversal.RefundAsync(paid, Reason, Ct));
        await Assert.ThrowsAsync<DomainException>(() => reversal.VoidAsync(paid, Reason, Ct));
        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Clerk_gets_forbidden_on_void_and_refund()
    {
        using var harness = new ServiceHarness();
        var id = await PaidSaleAsync(harness, quantity: 1);

        await Assert.ThrowsAsync<ForbiddenException>(() => harness.Reversal().VoidAsync(id, Reason, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => harness.Reversal().RefundAsync(id, Reason, Ct));
        Assert.Equal(TransactionStatus.Paid, (await harness.TransactionAsync(id)).Status);
    }

    [Fact]
    public async Task Manager_of_another_branch_gets_forbidden()
    {
        using var harness = new ServiceHarness();
        var id = await PaidSaleAsync(harness, quantity: 1);
        harness.As(harness.OtherManager);

        await Assert.ThrowsAsync<ForbiddenException>(() => harness.Reversal().VoidAsync(id, Reason, Ct));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reason_is_required(string reason)
    {
        using var harness = new ServiceHarness();
        var id = await PaidSaleAsync(harness, quantity: 1);
        harness.As(harness.Manager);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            harness.Reversal().VoidAsync(id, new ReversalCommand { Reason = reason }, Ct));

        Assert.Contains(nameof(ReversalCommand.Reason), error.Errors.Keys);
    }

    private static async Task<Guid> PaidSaleAsync(ServiceHarness harness, int quantity)
    {
        harness.As(harness.Clerk);
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku, quantity);
        await checkout.PayAsync(id, PaymentMethod.Card, Ct);
        return id;
    }
}
