using System.Text.RegularExpressions;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Security;

/// <summary>What the content security policy and section 11 require of every served page.</summary>
public sealed partial class PageMarkupTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    public static TheoryData<string> AdminPages => new()
    {
        "/",
        "/Catalogue",
        "/Transactions",
        "/Notifications",
        "/Reports",
        "/Reports/DailySales",
        "/Reports/LowStock",
        "/Reports/ShippingVolume",
        "/Reports/MoneyOrders",
        "/Staff",
        "/Staff/Create",
        "/Branches",
        "/Branches/Details/BR001",
        "/Audit",
    };

    [Theory]
    [MemberData(nameof(AdminPages))]
    public async Task Page_has_no_inline_code_and_a_title_header_and_skip_link(string path)
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        var page = await admin.ReadPageAsync(path);

        Assert.DoesNotMatch(InlineCode(), page);
        Assert.Matches("<title>[^<]+ · ApexPost Counter</title>", page);
        Assert.Contains("href=\"#main\"", page, StringComparison.Ordinal);
        Assert.Single(Heading().Matches(page));
    }

    [Fact]
    public async Task Counter_and_label_pages_have_no_inline_code()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var parcel = await counter.AddParcelAsync();

        var screen = await counter.PageAsync();
        var label = await clerk.ReadPageAsync(await counter.LabelLinkAsync());

        Assert.DoesNotMatch(InlineCode(), screen);
        Assert.DoesNotMatch(InlineCode(), label);
        Assert.Contains("Build the basket", screen, StringComparison.Ordinal);
        Assert.Single(Heading().Matches(label));
    }

    [Fact]
    public async Task Default_subtitle_names_the_branch()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        var page = await clerk.ReadPageAsync("/Transactions");

        Assert.Contains("<p class=\"muted\">Central Counter (BR001)", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Basket_of_a_closed_branch_offers_no_counter_forms()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, "clerk1.br004@apexpost.test");
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        var counter = await Counter.OpenAsync(clerk);
        var branch = await factory.BranchAsync("BR004");
        var version = new Dictionary<string, string> { ["version"] = branch.Version.ToString(null, null) };

        using var closed = await admin.PostAsync("/Branches", "/Branches/Close/BR004", version);
        var page = await counter.PageAsync();
        var newPage = await clerk.ReadPageAsync("/Transactions/New");

        Assert.Contains("This branch is closed.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Take payment", page, StringComparison.Ordinal);
        Assert.Contains("disabled=\"disabled\"", newPage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Voided_empty_basket_does_not_invite_more_items()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        var open = await counter.PageAsync();

        using var voided = await counter.For(manager).VoidAsync("Customer left.");
        var page = await counter.PageAsync();

        Assert.Contains("Add an item", open, StringComparison.Ordinal);
        Assert.DoesNotContain("Add an item", page, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\sstyle=""|\son[a-z]+=""|<style|<script(?![^>]*\ssrc=)", RegexOptions.IgnoreCase)]
    private static partial Regex InlineCode();

    [GeneratedRegex("<h1[ >]")]
    private static partial Regex Heading();
}
