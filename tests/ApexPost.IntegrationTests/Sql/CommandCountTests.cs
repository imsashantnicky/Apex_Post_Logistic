using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>
/// Reads the EF Core command log of real requests: a page runs a small fixed number of commands, however many rows it
/// shows. Each count includes the query that revalidates the signed-in user and, on pages that show it, the branch
/// name in the page header. An N+1 would grow with the rows.
/// </summary>
public sealed class CommandCountTests(CommandLogFactory factory) : IClassFixture<CommandLogFactory>
{
    [Theory]
    [InlineData(DemoAccounts.Manager, "/Dashboard", 4)]
    [InlineData(DemoAccounts.Manager, "/Catalogue", 2)]
    [InlineData(DemoAccounts.Manager, "/Transactions", 5)]
    [InlineData(DemoAccounts.Manager, "/Reports/DailySales", 3)]
    [InlineData(DemoAccounts.Manager, "/Reports/LowStock", 3)]
    [InlineData(DemoAccounts.Manager, "/Reports/ShippingVolume", 3)]
    [InlineData(DemoAccounts.Manager, "/Reports/MoneyOrders", 7)]
    [InlineData(DemoAccounts.Auditor, "/Reports/DailySales?branch=BR001", 4)]
    [InlineData(DemoAccounts.Auditor, "/Reports/DailySales", 3)]
    [InlineData(DemoAccounts.Auditor, "/Dashboard", 4)]
    public async Task Page_runs_a_fixed_number_of_commands(string email, string path, int commands)
    {
        using var browser = await CounterBrowser.SignInAsync(factory, email);

        using var page = await browser.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(commands, factory.CommandsOf(page));
    }

    [Fact]
    public async Task A_stock_adjustment_is_one_procedure_call()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        const string sku = "OFF-0005";

        using var adjusted = await manager.PostAsync(
            $"/Catalogue/Adjust?sku={sku}",
            "/Catalogue/Adjust",
            new Dictionary<string, string> { ["Sku"] = sku, ["Change"] = "1", ["Reason"] = "Delivery counted." });

        Assert.Equal(4, factory.CommandsOf(adjusted));
    }

    [Fact]
    public async Task All_branch_lists_cost_the_same_as_one_branch()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);

        using var oneBranch = await auditor.GetAsync("/Reports/MoneyOrders?branch=BR001");
        using var allBranches = await auditor.GetAsync("/Reports/MoneyOrders");

        Assert.Equal(factory.CommandsOf(oneBranch) - 1, factory.CommandsOf(allBranches));
    }

    [Fact]
    public async Task The_counter_screen_does_not_grow_with_the_basket()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var firstLine = await counter.AddRetailAsync("PKG-0001");
        using var small = await clerk.GetAsync(counter.Path);
        for (var line = 0; line < 8; line++)
        {
            using var added = await counter.AddRetailAsync("PKG-0002");
        }

        using var large = await clerk.GetAsync(counter.Path);

        Assert.Equal(factory.CommandsOf(small), factory.CommandsOf(large));
    }

    [Fact]
    public async Task Paying_a_big_basket_reads_all_its_stock_at_once()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var small = await BasketAsync(clerk, "PKG-0001");
        var large = await BasketAsync(clerk, "PKG-0001", "PKG-0002", "PKG-0003", "STA-0001", "STA-0002");

        using var paidSmall = await small.PayAsync();
        using var paidLarge = await large.PayAsync();

        Assert.Equal(factory.CommandsOf(paidSmall), factory.CommandsOf(paidLarge));
    }

    private static async Task<Counter> BasketAsync(CounterBrowser clerk, params string[] skus)
    {
        var counter = await Counter.OpenAsync(clerk);
        foreach (var sku in skus)
        {
            using var line = await counter.AddRetailAsync(sku);
        }

        return counter;
    }
}
