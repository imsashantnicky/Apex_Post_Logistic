using System.Net;
using ApexPost.Domain.Transactions;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>Voids and refunds as stored in SQL Server: who may do them and what is left behind.</summary>
public sealed class ReversalTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Sku = "PKG-0011";
    private const string Reason = "Customer changed their mind.";

    [Theory]
    [InlineData(DemoAccounts.Clerk)]
    [InlineData(DemoAccounts.Auditor)]
    [InlineData(DemoAccounts.OtherBranchManager)]
    public async Task Only_the_manager_of_the_branch_can_void_and_nothing_changes_otherwise(string email)
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var other = await CounterBrowser.SignInAsync(factory, email);
        var counter = await PaidSaleAsync(clerk);
        var stock = await factory.QuantityAsync(Sku);

        using var voided = await other.PostWithoutPageAsync($"/Transactions/Void/{counter.Id}", await FormAsync(other));

        Assert.Equal(HttpStatusCode.Forbidden, voided.StatusCode);
        var stored = await StoredTransaction.ReadAsync(factory, counter.Id);
        Assert.Equal(TransactionStatus.Paid, stored.Status);
        Assert.Null(stored.Reversal);
        Assert.Equal(stock, await factory.QuantityAsync(Sku));
    }

    [Fact]
    public async Task Refused_void_is_audited_as_forbidden()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await PaidSaleAsync(clerk);

        using var voided = await clerk.PostWithoutPageAsync($"/Transactions/Void/{counter.Id}", await FormAsync(clerk));

        var entry = await AuditTrail.EntryForAsync(factory, voided, AuditEvents.AccessDenied);
        Assert.Equal(403, entry.StatusCode);
    }

    [Fact]
    public async Task Void_of_a_paid_sale_keeps_the_reason_and_returns_the_stock_in_one_commit()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var before = await factory.QuantityAsync(Sku);
        var counter = await PaidSaleAsync(clerk);

        using var voided = await counter.For(manager).VoidAsync(Reason);

        Assert.Equal(HttpStatusCode.Redirect, voided.StatusCode);
        var stored = await StoredTransaction.ReadAsync(factory, counter.Id);
        Assert.Equal(TransactionStatus.Voided, stored.Status);
        Assert.Equal(Reason, stored.Reversal?.Reason);
        Assert.Equal((await factory.StaffAsync(DemoAccounts.Manager)).Id, stored.Reversal?.ManagerId);
        Assert.Equal(before, await factory.QuantityAsync(Sku));
    }

    [Fact]
    public async Task Void_of_an_open_basket_moves_no_stock()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(Sku);
        var before = await factory.QuantityAsync(Sku);

        using var voided = await counter.For(manager).VoidAsync(Reason);

        Assert.Equal(TransactionStatus.Voided, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
        Assert.Equal(before, await factory.QuantityAsync(Sku));
    }

    [Fact]
    public async Task Refund_returns_the_stock_keeps_the_payment_and_can_happen_only_once()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var before = await factory.QuantityAsync(Sku);
        var counter = await PaidSaleAsync(clerk);

        using var refunded = await counter.For(manager).RefundAsync(Reason);
        var stockAfterRefund = await factory.QuantityAsync(Sku);
        using var again = await counter.For(manager).RefundAsync(Reason);

        var stored = await StoredTransaction.ReadAsync(factory, counter.Id);
        Assert.Equal(TransactionStatus.Refunded, stored.Status);
        Assert.NotNull(stored.Payment);
        Assert.Equal(Reason, stored.Reversal?.Reason);
        Assert.Equal(before, stockAfterRefund);
        Assert.Equal(before, await factory.QuantityAsync(Sku));
        var audit = await AuditTrail.EntryForAsync(factory, again, AuditEvents.Refund);
        Assert.Equal(400, audit.StatusCode);
    }

    [Fact]
    public async Task An_open_basket_cannot_be_refunded()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(Sku);

        using var refunded = await counter.For(manager).RefundAsync(Reason);

        Assert.Equal(TransactionStatus.Open, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
    }

    [Fact]
    public async Task A_reversal_needs_a_reason()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var counter = await PaidSaleAsync(clerk);

        using var voided = await counter.For(manager).VoidAsync("   ");

        Assert.Equal(TransactionStatus.Paid, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
    }

    private static async Task<Counter> PaidSaleAsync(CounterBrowser clerk)
    {
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(Sku);
        using var paid = await counter.PayAsync();
        return counter;
    }

    /// <summary>A valid token of this browser, so that only the authorization can refuse the post.</summary>
    private static async Task<Dictionary<string, string>> FormAsync(CounterBrowser browser) =>
        new()
        {
            ["Reason"] = Reason,
            ["__RequestVerificationToken"] = await browser.TokenFromAsync(Counter.TransactionsPath),
        };
}
