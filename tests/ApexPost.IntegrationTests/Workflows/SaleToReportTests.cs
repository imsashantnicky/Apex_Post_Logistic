using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApexPost.Domain.Transactions;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Workflows;

/// <summary>
/// One sale from the browser to the database and into the report: every row it should leave, the exact change of the
/// report totals worked out by hand, and who may reverse it.
/// </summary>
public sealed partial class SaleToReportTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Clerk = "clerk1.br004@apexpost.test";
    private const string Manager = "manager.br004@apexpost.test";
    private const string Branch = "BR004";
    private const string Sku = "PKG-0002";
    private const string ProblemJson = "application/problem+json";
    private const string Reason = "Sold by mistake.";
    private const int Quantity = 3;
    private const decimal ParcelKilograms = 2m;
    private const decimal OrderAmount = 100m;
    private const decimal OrderFee = 2.00m;
    private const decimal TaxRate = 0.20m;

    private static string ReportPath
    {
        get
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return $"/Reports/DailySales?from={today}&to={today}";
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_sale_leaves_every_row_it_should()
    {
        var stockBefore = await factory.QuantityAsync(Sku, Branch);

        var counter = await SellAsync();

        Assert.Equal(1, await CountAsync("TransactionLines", counter));
        Assert.Equal(1, await CountAsync("Shipments", counter));
        Assert.Equal(1, await CountAsync("MoneyOrders", counter));
        Assert.Equal(1, await CountAsync("Payments", counter));
        Assert.Equal(stockBefore - Quantity, await factory.QuantityAsync(Sku, Branch));
        var stored = await StoredTransaction.ReadAsync(factory, counter.Id);
        Assert.Equal(TransactionStatus.Paid, stored.Status);
        Assert.Equal(PaymentMethod.Card, stored.Payment?.Method);
    }

    [Fact]
    public async Task The_daily_sales_report_grows_by_exactly_the_sale()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, Manager);
        var before = Totals.Read(await manager.ReadPageAsync(ReportPath));
        var retail = await PriceAsync() * Quantity;
        var services = await ParcelPriceAsync() + OrderAmount + OrderFee;
        var tax = Math.Round((retail * TaxRate) + (await ParcelPriceAsync() * TaxRate), 2, MidpointRounding.AwayFromZero);

        await SellAsync();

        var after = Totals.Read(await manager.ReadPageAsync(ReportPath));
        Assert.Equal(before.Transactions + 1, after.Transactions);
        Assert.Equal(before.Retail + retail, after.Retail);
        Assert.Equal(before.Services + services, after.Services);
        Assert.Equal(before.Tax + tax, after.Tax);
        Assert.Equal(before.Total + retail + services + tax, after.Total);
    }

    [Fact]
    public async Task A_refund_takes_exactly_that_sale_out_of_the_report_again()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, Manager);
        var before = Totals.Read(await manager.ReadPageAsync(ReportPath));
        var counter = await SellAsync();

        using var refunded = await counter.For(manager).RefundAsync(Reason);

        Assert.Equal(before, Totals.Read(await manager.ReadPageAsync(ReportPath)));
    }

    [Fact]
    public async Task The_branch_manager_may_void_and_the_void_leaves_the_report()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, Manager);
        var before = Totals.Read(await manager.ReadPageAsync(ReportPath));
        var counter = await SellAsync();

        using var voided = await counter.For(manager).VoidAsync(Reason);

        Assert.Equal(HttpStatusCode.Redirect, voided.StatusCode);
        Assert.Equal(TransactionStatus.Voided, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
        Assert.Equal(before, Totals.Read(await manager.ReadPageAsync(ReportPath)));
    }

    [Fact]
    public async Task A_clerk_who_posts_a_void_gets_a_403_problem()
    {
        var counter = await SellAsync();
        using var clerk = await CounterBrowser.SignInAsync(factory, Clerk);
        clerk.AcceptJson();

        using var refused = await counter.For(clerk).VoidAsync(Reason);

        await AssertProblemAsync(refused, HttpStatusCode.Forbidden);
        Assert.Equal(TransactionStatus.Paid, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
    }

    [Fact]
    public async Task A_manager_of_another_branch_who_posts_a_void_gets_a_403_problem()
    {
        var counter = await SellAsync();
        using var other = await CounterBrowser.SignInAsync(factory, DemoAccounts.OtherBranchManager);
        var ownPage = await Counter.OpenAsync(other);
        other.AcceptJson();

        using var refused = await ownPage.VoidOtherAsync(counter.Id, Reason);

        await AssertProblemAsync(refused, HttpStatusCode.Forbidden);
        Assert.Equal(TransactionStatus.Paid, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
    }

    [Fact]
    public async Task A_void_without_a_reason_is_a_400_problem_with_the_field()
    {
        var counter = await SellAsync();
        using var manager = await CounterBrowser.SignInAsync(factory, Manager);
        manager.AcceptJson();

        using var refused = await counter.For(manager).VoidAsync(string.Empty);

        await AssertProblemAsync(refused, HttpStatusCode.BadRequest);
        Assert.Equal(TransactionStatus.Paid, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
    }

    [Fact]
    public async Task Paying_an_unknown_transaction_is_a_404_problem()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, Manager);
        manager.AcceptJson();

        var counter = await Counter.OpenAsync(manager);

        using var refused = await counter.PayOtherAsync(Guid.NewGuid());

        await AssertProblemAsync(refused, HttpStatusCode.NotFound);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("correlationId").GetString()));
        Assert.False(problem.TryGetProperty("stackTrace", out _));
    }

    private async Task<Counter> SellAsync()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var retail = await counter.AddRetailAsync(Sku, Quantity);
        using var parcel = await counter.AddParcelAsync(ParcelKilograms);
        using var order = await counter.AddMoneyOrderAsync(OrderAmount);
        using var paid = await counter.PayAsync();
        Assert.Equal(TransactionStatus.Paid, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
        return counter;
    }

    private Task<int> CountAsync(string table, Counter counter) =>
        factory.ScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE TransactionId = '{counter.Id}'");

    private Task<decimal> PriceAsync() =>
        factory.ScalarAsync<decimal>($"SELECT UnitPrice FROM Products WHERE Sku = '{Sku}'");

    /// <summary>Zone Z01 at the Standard level: the parcel's 2 kg weigh more than its 1.2 kg of volume.</summary>
    private async Task<decimal> ParcelPriceAsync()
    {
        var baseRate = await factory.ScalarAsync<decimal>("SELECT BaseRate FROM ShippingRates WHERE ZoneCode = 'Z01'");
        var perKg = await factory.ScalarAsync<decimal>("SELECT PerKgRate FROM ShippingRates WHERE ZoneCode = 'Z01'");
        return Math.Round(baseRate + (perKg * ParcelKilograms), 2, MidpointRounding.AwayFromZero);
    }

    [GeneratedRegex("<tfoot>(.*?)</tfoot>", RegexOptions.Singleline)]
    private static partial Regex Footer();

    [GeneratedRegex("""<td class="num">(\d+)</td>""")]
    private static partial Regex Count();

    [GeneratedRegex("""<span class="money">([\d.,]+) USD</span>""")]
    private static partial Regex Amount();

    private sealed record Totals(int Transactions, decimal Retail, decimal Services, decimal Tax, decimal Total)
    {
        /// <summary>The footer row of the report, or zeros when the page shows no sales yet.</summary>
        public static Totals Read(string page)
        {
            var footer = Footer().Match(page);
            if (!footer.Success)
            {
                return new Totals(0, 0m, 0m, 0m, 0m);
            }

            var amounts = Amount().Matches(footer.Value)
                .Select(match => decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
                .ToArray();
            return new Totals(
                int.Parse(Count().Match(footer.Value).Groups[1].Value, CultureInfo.InvariantCulture),
                amounts[0],
                amounts[1],
                amounts[2],
                amounts[3]);
        }
    }
}
