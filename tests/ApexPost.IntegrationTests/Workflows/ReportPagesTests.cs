using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Workflows;

public sealed partial class ReportPagesTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Sku = "PKG-0001";
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static string TodayText => Date(Today);

    [Fact]
    public async Task Daily_sales_of_the_seeded_history_cover_all_open_branches_for_an_auditor()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);

        var page = await auditor.ReadPageAsync("/Reports/DailySales");

        Assert.Contains("All branches", page, StringComparison.Ordinal);
        foreach (var branch in new[] { "BR001", "BR002", "BR003", "BR004", "BR005" })
        {
            Assert.Contains($"<td>{branch}</td>", page, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("<td>BR006</td>", page, StringComparison.Ordinal);
        Assert.Contains("<option value=\"BR006\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Branch_filter_limits_every_row_to_that_branch()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);

        var sales = await auditor.ReadPageAsync("/Reports/DailySales?branch=BR002");
        var stock = await auditor.ReadPageAsync("/Reports/LowStock?branch=BR002");
        var orders = await auditor.ReadPageAsync("/Reports/MoneyOrders?branch=BR002");

        Assert.All(new[] { sales, orders, stock }, page =>
        {
            Assert.Contains("Harbour Point (BR002)", page, StringComparison.Ordinal);
            Assert.Contains("<td>BR002</td>", page, StringComparison.Ordinal);
            Assert.DoesNotContain("<td>BR001</td>", page, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Date_filter_finds_a_sale_of_today_and_nothing_for_tomorrow()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        using var added = await counter.AddParcelAsync();
        using var paid = await counter.PayAsync();
        var tomorrow = Date(Today.AddDays(1));

        var sales = await manager.ReadPageAsync($"/Reports/DailySales?from={TodayText}&to={TodayText}");
        var empty = await manager.ReadPageAsync($"/Reports/DailySales?from={tomorrow}&to={tomorrow}");
        var shipping = await manager.ReadPageAsync($"/Reports/ShippingVolume?from={TodayText}&to={TodayText}");

        Assert.Contains($"<td>{TodayText}</td>", sales, StringComparison.Ordinal);
        Assert.DoesNotContain($"<td>{Date(Today.AddDays(-1))}</td>", sales, StringComparison.Ordinal);
        Assert.Contains("No paid sales in this period.", empty, StringComparison.Ordinal);
        Assert.Contains("<td>Z01</td>", shipping, StringComparison.Ordinal);
        Assert.Contains($"value=\"{TodayText}\"", shipping, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refunded_sale_leaves_the_daily_sales()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, "clerk1.br003@apexpost.test");
        using var manager = await CounterBrowser.SignInAsync(factory, "manager.br003@apexpost.test");
        var counter = await Counter.OpenAsync(clerk);
        using var added = await counter.AddRetailAsync(Sku);
        using var paid = await counter.PayAsync();
        var path = $"/Reports/DailySales?from={TodayText}&to={TodayText}";
        var before = await manager.ReadPageAsync(path);

        using var refunded = await counter.For(manager).RefundAsync("Damaged item.");

        Assert.Contains($"<td>{TodayText}</td>", before, StringComparison.Ordinal);
        Assert.Contains("No paid sales in this period.", await manager.ReadPageAsync(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Low_stock_shows_the_threshold_and_has_no_date_filter()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);

        var page = await manager.ReadPageAsync("/Reports/LowStock");

        Assert.Contains("items with 5 or fewer left", page, StringComparison.Ordinal);
        Assert.Contains("Threshold", page, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"date\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shipping_volume_is_grouped_by_zone_and_service_level()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);

        var page = await auditor.ReadPageAsync("/Reports/ShippingVolume");

        Assert.Contains("Chargeable kg", page, StringComparison.Ordinal);
        Assert.Contains("<td>Express</td>", page, StringComparison.Ordinal);
        Assert.Contains("<td>Registered</td>", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Money_order_report_has_totals_per_branch_and_only_masked_numbers()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);

        var page = await auditor.ReadPageAsync("/Reports/MoneyOrders");

        Assert.Contains("Per branch", page, StringComparison.Ordinal);
        Assert.Contains("Checks passed", page, StringComparison.Ordinal);
        Assert.Contains("****", page, StringComparison.Ordinal);
        Assert.DoesNotMatch(FullSeededDocumentNumber(), page);
    }

    [Fact]
    public async Task Money_order_pages_keep_the_filter()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);
        var from = Date(Today.AddDays(-14));

        var page = await auditor.ReadPageAsync($"/Reports/MoneyOrders?from={from}&to={TodayText}");

        var next = WebUtility.HtmlDecode(NextPageLink().Match(page).Groups[1].Value);
        Assert.Contains($"from={from}", next, StringComparison.Ordinal);
        Assert.Contains($"to={TodayText}", next, StringComparison.Ordinal);
        var second = await auditor.ReadPageAsync(next);
        Assert.Contains("Page 2 of", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dates_the_wrong_way_round_show_one_day_instead_of_failing()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);
        var earlier = Date(Today.AddDays(-5));

        var page = await auditor.ReadPageAsync($"/Reports/DailySales?from={TodayText}&to={earlier}");

        Assert.Contains($"{TodayText} to {TodayText}", page, StringComparison.Ordinal);
    }

    private static string Date(DateOnly day) => day.ToString(DateFormat, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\bD1\d{6}\b")]
    private static partial Regex FullSeededDocumentNumber();

    [GeneratedRegex("<a href=\"([^\"]+)\">Next</a>")]
    private static partial Regex NextPageLink();
}
