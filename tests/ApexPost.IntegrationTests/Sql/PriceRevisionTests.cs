using System.Globalization;
using System.Net;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Sql;

public sealed class PriceRevisionTests(CommandLogFactory factory) : IClassFixture<CommandLogFactory>
{
    private const string CataloguePath = "/Catalogue";
    private const string RevisePath = "/Catalogue/RevisePrices";

    [Fact]
    public async Task One_statement_changes_one_category_and_the_next_sale_uses_the_new_price()
    {
        var before = await PricesAsync();
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        await counter.PageAsync();

        using var revised = await ReviseAsync(admin, "Stationery", 12.5m);

        Assert.Equal(HttpStatusCode.Redirect, revised.StatusCode);
        var after = await PricesAsync();
        foreach (var (sku, category, price) in before)
        {
            var expected = category == ProductCategory.Stationery
                ? Math.Round(price * 1.125m, 2, MidpointRounding.AwayFromZero)
                : price;
            Assert.Equal(expected, after.Single(row => row.Sku == sku).Price);
        }

        var entry = await AuditTrail.EntryForAsync(factory, revised, AuditEvents.PriceRevision);
        Assert.Equal("12.5", entry.RouteValues["percent"]);
        Assert.Equal("Stationery", entry.RouteValues["category"]);

        var changed = before.First(row => row.Category == ProductCategory.Stationery);
        using var line = await counter.AddRetailAsync(changed.Sku.Value);
        var newPrice = after.Single(row => row.Sku == changed.Sku).Price;
        Assert.Contains(
            newPrice.ToString("0.00", CultureInfo.InvariantCulture),
            await counter.PageAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_basket_keeps_the_price_it_was_built_with()
    {
        var sale = (await PricesAsync()).First(row => row.Category == ProductCategory.OfficeSupplies);
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(sale.Sku.Value);
        var page = await counter.PageAsync();

        using var revised = await ReviseAsync(admin, "OfficeSupplies", 50m);

        Assert.Contains(
            sale.Price.ToString("0.00", CultureInfo.InvariantCulture),
            await counter.PageAsync(),
            StringComparison.Ordinal);
        Assert.Contains(sale.Price.ToString("0.00", CultureInfo.InvariantCulture), page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DemoAccounts.Manager)]
    [InlineData(DemoAccounts.Clerk)]
    public async Task Other_roles_are_refused_and_nothing_changes(string email)
    {
        var before = await PricesAsync();
        using var browser = await AdminBrowser.SignInAsync(factory, email);

        using var revised = await ReviseAsync(browser, "Packaging", 10m);

        Assert.Equal(HttpStatusCode.Forbidden, revised.StatusCode);
        Assert.Equal(before, await PricesAsync());
    }

    [Fact]
    public async Task A_percent_over_the_limit_changes_nothing_and_shows_why()
    {
        var before = await PricesAsync();
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var revised = await ReviseAsync(admin, "Packaging", 51m);

        Assert.Equal(HttpStatusCode.Redirect, revised.StatusCode);
        Assert.Equal(before, await PricesAsync());
        Assert.Contains("Percent must be between", await admin.ReadPageAsync(CataloguePath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_administrators_see_the_form()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        Assert.DoesNotContain("Revise prices", await manager.ReadPageAsync(CataloguePath), StringComparison.Ordinal);
        Assert.Contains("Revise prices", await admin.ReadPageAsync(CataloguePath), StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> ReviseAsync(CounterBrowser browser, string category, decimal percent) =>
        browser.PostAsync(
            CataloguePath,
            RevisePath,
            new Dictionary<string, string>
            {
                ["category"] = category,
                ["percent"] = percent.ToString(CultureInfo.InvariantCulture),
                ["reason"] = "Yearly price review.",
            });

    private async Task<List<(Sku Sku, ProductCategory Category, decimal Price)>> PricesAsync()
    {
        await using var database = factory.OpenDatabase();
        var rows = await database.Set<Product>()
            .AsNoTracking()
            .OrderBy(product => product.Id)
            .Select(product => new { product.Id, product.Category, product.UnitPrice })
            .ToListAsync();
        return [.. rows.Select(row => (row.Id, row.Category, ((Money)row.UnitPrice).Amount))];
    }
}
