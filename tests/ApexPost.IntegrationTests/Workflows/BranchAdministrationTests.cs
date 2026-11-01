using System.Globalization;
using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Workflows;

public sealed class BranchAdministrationTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string BranchesPath = "/Branches";
    private const string Sku = "PKG-0001";

    [Fact]
    public async Task List_shows_every_branch_with_hours_counters_and_status()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        var page = await admin.ReadPageAsync(BranchesPath);

        Assert.Contains("Central Counter", page, StringComparison.Ordinal);
        Assert.Contains("08:00–18:00", page, StringComparison.Ordinal);
        Assert.Contains("pill-closed", page, StringComparison.Ordinal);
        Assert.Contains("/Branches/Details/BR006", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Closing_refuses_new_baskets_and_reopening_allows_them_again()
    {
        const string code = "BR004";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var clerk = await CounterBrowser.SignInAsync(factory, "clerk1.br004@apexpost.test");

        using var closed = await ChangeAsync(admin, code, "Close");
        var details = await admin.ReadPageAsync(closed.Headers.Location!.OriginalString);
        using var refused = await Counter.PostOpenAsync(clerk);
        var flash = await clerk.ReadPageAsync(refused.Headers.Location!.OriginalString);
        using var reopened = await ChangeAsync(admin, code, "Reopen");

        Assert.Contains("Branch closed.", details, StringComparison.Ordinal);
        Assert.Contains("is closed and cannot accept transactions", flash, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, reopened.StatusCode);
        await Counter.OpenAsync(clerk);
    }

    [Fact]
    public async Task Basket_already_open_cannot_be_paid_once_the_branch_closes_but_can_be_voided()
    {
        const string code = "BR005";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var clerk = await CounterBrowser.SignInAsync(factory, "clerk2.br005@apexpost.test");
        using var manager = await CounterBrowser.SignInAsync(factory, "manager.br005@apexpost.test");
        var counter = await Counter.OpenAsync(clerk);
        using var added = await counter.AddRetailAsync(Sku);
        var stock = await factory.QuantityAsync(Sku, code);

        using var closed = await ChangeAsync(admin, code, "Close");
        using var paid = await counter.PayAsync();
        var page = await counter.PageAsync();
        using var voided = await counter.For(manager).VoidAsync("Branch closed for the day.");
        using var reopened = await ChangeAsync(admin, code, "Reopen");

        Assert.Contains("is closed and cannot accept transactions", page, StringComparison.Ordinal);
        Assert.Contains("pill-open", page, StringComparison.Ordinal);
        Assert.Equal(stock, await factory.QuantityAsync(Sku, code));
        Assert.Contains("Transaction voided.", await counter.For(manager).PageAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Added_counter_is_offered_to_clerks_and_a_removed_one_is_not()
    {
        const string code = "BR003";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var clerk = await CounterBrowser.SignInAsync(factory, "clerk1.br003@apexpost.test");

        using var added = await ChangeAsync(admin, code, "AddCounter");
        var withNew = await clerk.ReadPageAsync("/Transactions/New");
        var branch = await factory.BranchAsync(code);
        using var removed = await admin.PostAsync(
            $"{BranchesPath}/Details/{code}",
            $"{BranchesPath}/RemoveCounter/{code}",
            new Dictionary<string, string>
            {
                ["counterId"] = branch.Counters[^1].Id.ToString(),
                ["version"] = Text(branch.Version),
            });
        var withoutNew = await clerk.ReadPageAsync("/Transactions/New");

        Assert.Contains("Counter 4", withNew, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.DoesNotContain("Counter 4", withoutNew, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Counter_with_an_open_basket_stays_and_the_administrator_is_told_why()
    {
        const string code = "BR002";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.OtherBranchClerk);
        await Counter.OpenAsync(clerk);
        var branch = await factory.BranchAsync(code);

        using var response = await admin.PostAsync(
            $"{BranchesPath}/Details/{code}",
            $"{BranchesPath}/RemoveCounter/{code}",
            new Dictionary<string, string>
            {
                ["counterId"] = branch.Counters[0].Id.ToString(),
                ["version"] = Text(branch.Version),
            });

        var page = await admin.ReadPageAsync(response.Headers.Location!.OriginalString);
        Assert.Contains("has an open basket", page, StringComparison.Ordinal);
        Assert.Equal(branch.Counters.Count, (await factory.BranchAsync(code)).Counters.Count);
    }

    [Fact]
    public async Task Last_counter_has_no_remove_button_and_cannot_be_removed()
    {
        const string code = "BR006";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        var branch = await factory.BranchAsync(code);

        var page = await admin.ReadPageAsync($"{BranchesPath}/Details/{code}");
        using var response = await admin.PostAsync(
            BranchesPath,
            $"{BranchesPath}/RemoveCounter/{code}",
            new Dictionary<string, string>
            {
                ["counterId"] = branch.Counters[0].Id.ToString(),
                ["version"] = Text(branch.Version),
            });

        Assert.DoesNotContain("RemoveCounter", page, StringComparison.Ordinal);
        Assert.Single((await factory.BranchAsync(code)).Counters);
        Assert.Equal($"{BranchesPath}/Details/{code}", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("BR999")]
    public async Task Unknown_branch_is_not_found(string code)
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var response = await admin.GetAsync($"{BranchesPath}/Details/{code}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    private async Task<HttpResponseMessage> ChangeAsync(CounterBrowser admin, string code, string action)
    {
        var branch = await factory.BranchAsync(code);
        return await admin.PostAsync(
            $"{BranchesPath}/Details/{code}",
            $"{BranchesPath}/{action}/{code}",
            new Dictionary<string, string> { ["version"] = Text(branch.Version) });
    }
}
