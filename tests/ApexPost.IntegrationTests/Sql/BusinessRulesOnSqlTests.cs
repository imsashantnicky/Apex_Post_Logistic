using System.Globalization;
using ApexPost.Domain.Transactions;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>
/// The boundaries of the Week 2 rules, walked through the real application and stored in SQL Server. The Week 2 unit
/// tests prove them on fakes; these prove that nothing was lost on the way through EF Core and the constraints.
/// </summary>
public sealed class BusinessRulesOnSqlTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Reason = "Customer changed their mind.";

    [Theory]
    [InlineData(299.99, TransactionStatus.Paid)]
    [InlineData(300.00, TransactionStatus.Paid)]
    [InlineData(300.01, TransactionStatus.Open)]
    public async Task Rule4_money_order_threshold(double amount, TransactionStatus expected)
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var order = await counter.AddMoneyOrderAsync((decimal)amount);

        using var paid = await counter.PayAsync();

        Assert.Equal(expected, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
    }

    [Theory]
    [InlineData("P12345678", TransactionStatus.Paid)]
    [InlineData("FAIL5678", TransactionStatus.Open)]
    [InlineData("PEND5678", TransactionStatus.Open)]
    public async Task Rule4_only_a_passed_check_in_the_same_transaction_counts(
        string document,
        TransactionStatus expected)
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var order = await counter.AddMoneyOrderAsync(300.01m);
        using var check = await counter.VerifyAsync(document);

        using var paid = await counter.PayAsync();

        Assert.Equal(expected, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
        Assert.Equal("5678", await factory.ScalarAsync<string>(
            $"SELECT DocumentLast4 FROM IdentityVerifications WHERE TransactionId = '{counter.Id}'"));
    }

    [Theory]
    [InlineData("Standard", "Parcel", 30, 1)]
    [InlineData("Standard", "Parcel", 30.001, 0)]
    [InlineData("Express", "Parcel", 20, 1)]
    [InlineData("Express", "Parcel", 20.001, 0)]
    [InlineData("Registered", "Parcel", 10, 1)]
    [InlineData("Registered", "Parcel", 10.001, 0)]
    [InlineData("Standard", "Letter", 2, 1)]
    [InlineData("Standard", "Letter", 2.001, 0)]
    public async Task Rule7_weight_limits_decide_whether_a_row_is_stored(
        string level,
        string type,
        double kilograms,
        int rows)
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var added = await counter.AddParcelAsync((decimal)kilograms, level, type);

        Assert.Equal(rows, await factory.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM Shipments WHERE TransactionId = '{counter.Id}'"));
    }

    [Fact]
    public async Task Rule6_selling_the_exact_stock_leaves_zero_and_one_more_changes_nothing()
    {
        const string sku = "OFF-0004";
        await factory.SetQuantityAsync(sku, 3);
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var exact = await Counter.OpenAsync(clerk);
        using var line = await exact.AddRetailAsync(sku, 3);
        using var sold = await exact.PayAsync();
        var tooMany = await Counter.OpenAsync(clerk);
        using var overLine = await tooMany.AddRetailAsync(sku);

        using var refused = await tooMany.PayAsync();

        Assert.Equal(0, await factory.QuantityAsync(sku));
        Assert.Equal(TransactionStatus.Paid, (await StoredTransaction.ReadAsync(factory, exact.Id)).Status);
        var open = await StoredTransaction.ReadAsync(factory, tooMany.Id);
        Assert.Equal(TransactionStatus.Open, open.Status);
        Assert.Null(open.Payment);
    }

    [Fact]
    public async Task Rule6_one_short_line_stops_the_whole_payment()
    {
        const string plenty = "OFF-0005";
        const string short1 = "OFF-0006";
        await factory.SetQuantityAsync(plenty, 9);
        await factory.SetQuantityAsync(short1, 1);
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var first = await counter.AddRetailAsync(plenty, 2);
        using var second = await counter.AddRetailAsync(short1, 2);

        using var refused = await counter.PayAsync();

        Assert.Equal(9, await factory.QuantityAsync(plenty));
        Assert.Equal(1, await factory.QuantityAsync(short1));
    }

    [Theory]
    [InlineData("Void")]
    [InlineData("Refund")]
    public async Task Rule5_a_reversal_returns_the_stock_and_keeps_the_payment_and_reason(string kind)
    {
        const string sku = "OFF-0007";
        await factory.SetQuantityAsync(sku, 8);
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(sku, 2);
        using var paid = await counter.PayAsync();
        Assert.Equal(6, await factory.QuantityAsync(sku));

        using var reversed = kind == "Void"
            ? await counter.For(manager).VoidAsync(Reason)
            : await counter.For(manager).RefundAsync(Reason);

        var stored = await StoredTransaction.ReadAsync(factory, counter.Id);
        Assert.Equal(8, await factory.QuantityAsync(sku));
        Assert.Equal(kind == "Void" ? TransactionStatus.Voided : TransactionStatus.Refunded, stored.Status);
        Assert.Equal(Reason, stored.Reversal?.Reason);
        Assert.NotNull(stored.Payment);
    }

    [Fact]
    public async Task Rule5_a_clerk_changes_no_row_by_posting_a_void()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync("OFF-0008");
        using var paid = await counter.PayAsync();

        using var refused = await counter.VoidAsync(Reason);

        var stored = await StoredTransaction.ReadAsync(factory, counter.Id);
        Assert.Equal(TransactionStatus.Paid, stored.Status);
        Assert.Null(stored.Reversal);
    }

    [Fact]
    public async Task Rule9_tax_is_previewed_on_the_open_basket_and_frozen_in_the_payment_row()
    {
        const string sku = "STA-0005";
        var price = await factory.ScalarAsync<decimal>($"SELECT UnitPrice FROM Products WHERE Sku = '{sku}'");
        var category = await factory.ScalarAsync<string>($"SELECT TaxCategory FROM Products WHERE Sku = '{sku}'");
        var rate = category switch { "Standard" => 0.20m, "Reduced" => 0.05m, _ => 0m };
        var expected = Math.Round(price * 2 * rate, 2, MidpointRounding.AwayFromZero);
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(sku, 2);

        var preview = await counter.PageAsync();
        using var paid = await counter.PayAsync();

        Assert.Contains(expected.ToString("0.00", CultureInfo.InvariantCulture) + " USD", preview, StringComparison.Ordinal);
        Assert.Equal(expected, await factory.ScalarAsync<decimal>(
            $"SELECT Tax FROM Payments WHERE TransactionId = '{counter.Id}'"));
    }

    [Fact]
    public async Task Rule10_the_money_order_fee_is_stored_at_both_sides_of_its_minimum()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var small = await Counter.OpenAsync(clerk);
        using var first = await small.AddMoneyOrderAsync(50m);
        var large = await Counter.OpenAsync(clerk);
        using var second = await large.AddMoneyOrderAsync(250m);

        Assert.Equal(2.00m, await factory.ScalarAsync<decimal>(
            $"SELECT Fee FROM MoneyOrders WHERE TransactionId = '{small.Id}'"));
        Assert.Equal(5.00m, await factory.ScalarAsync<decimal>(
            $"SELECT Fee FROM MoneyOrders WHERE TransactionId = '{large.Id}'"));
    }
}
