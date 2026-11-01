using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Security;

public sealed class AdminAccessTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string LoginPath = "/Account/Login";

    public static TheoryData<string> AdminPages => new()
    {
        "/Staff",
        "/Staff/Create",
        "/Branches",
        "/Branches/Details/BR001",
    };

    public static TheoryData<string> AdminForms => new()
    {
        "/Staff/Create",
        $"/Staff/Edit/{Guid.Empty}",
        $"/Staff/SetPassword/{Guid.Empty}",
        $"/Staff/Deactivate/{Guid.Empty}",
        $"/Staff/Activate/{Guid.Empty}",
        "/Branches/Close/BR001",
        "/Branches/Reopen/BR001",
        "/Branches/AddCounter/BR001",
        "/Branches/RemoveCounter/BR001",
    };

    public static TheoryData<string> Reports => new()
    {
        "/Reports",
        "/Reports/DailySales",
        "/Reports/LowStock",
        "/Reports/ShippingVolume",
        "/Reports/MoneyOrders",
    };

    public static TheoryData<string> BranchReports => new()
    {
        "/Reports/DailySales?branch=BR002",
        "/Reports/LowStock?branch=BR002",
        "/Reports/ShippingVolume?branch=BR002",
        "/Reports/MoneyOrders?branch=BR002",
        "/Notifications?branch=BR002",
    };

    [Theory]
    [InlineData(DemoAccounts.Clerk)]
    [InlineData(DemoAccounts.Manager)]
    [InlineData(DemoAccounts.Auditor)]
    public async Task Other_roles_get_403_on_every_admin_page_even_from_an_allowed_address(string email)
    {
        using var browser = await AdminBrowser.SignInAsync(factory, email);

        foreach (var path in AdminPages)
        {
            using var response = await browser.GetAsync(path);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Theory]
    [MemberData(nameof(AdminForms))]
    public async Task Clerk_gets_403_on_every_admin_form(string path)
    {
        using var clerk = await AdminBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var response = await clerk.PostAsync("/", path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminPages))]
    public async Task Administrator_on_a_blocked_address_gets_403(string path)
    {
        using var admin = await CounterBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var response = await admin.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminForms))]
    public async Task Administrator_on_a_blocked_address_cannot_post_either(string path)
    {
        using var admin = await CounterBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var response = await admin.PostAsync("/", path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True((await factory.BranchAsync("BR001")).IsActive);
    }

    [Theory]
    [MemberData(nameof(AdminPages))]
    public async Task Administrator_on_an_allowed_address_opens_the_page(string path)
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var response = await admin.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_form_without_an_antiforgery_token_is_rejected()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var response = await admin.PostWithoutPageAsync(
            "/Branches/Close/BR001",
            new Dictionary<string, string> { ["version"] = "1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await factory.BranchAsync("BR001")).IsActive);
    }

    [Theory]
    [InlineData("/Staff")]
    [InlineData("/Branches")]
    [InlineData("/Reports")]
    [InlineData("/Notifications")]
    public async Task Signed_out_visitor_on_an_allowed_address_goes_to_the_login_page(string path)
    {
        using var visitor = new CounterBrowser(factory);
        visitor.CallFrom(PartnerClient.AllowedAddress);

        using var response = await visitor.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(LoginPath, response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Reports))]
    public async Task Clerk_gets_403_on_the_reports(string path)
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var response = await clerk.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(BranchReports))]
    public async Task Manager_gets_403_when_naming_another_branch(string path)
    {
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);

        using var response = await manager.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(BranchReports))]
    public async Task Auditor_reads_any_branch_from_any_address(string path)
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);

        using var response = await auditor.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manager_report_stays_on_the_own_branch_and_offers_no_other()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);

        var page = await manager.ReadPageAsync("/Reports/DailySales");

        Assert.Contains("Central Counter (BR001)", page, StringComparison.Ordinal);
        Assert.Contains("<td>BR001</td>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>BR002</td>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("All branches", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<select id=\"branch\"", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DemoAccounts.Administrator)]
    [InlineData(DemoAccounts.Auditor)]
    [InlineData(DemoAccounts.Manager)]
    public async Task Reports_open_for_the_report_viewers(string email)
    {
        using var browser = await CounterBrowser.SignInAsync(factory, email);

        foreach (var path in Reports)
        {
            using var response = await browser.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("/Reports/DailySales?branch=nonsense", HttpStatusCode.BadRequest)]
    [InlineData("/Reports/DailySales?branch=BR999", HttpStatusCode.NotFound)]
    [InlineData("/Reports/DailySales?from=yesterday", HttpStatusCode.BadRequest)]
    [InlineData("/Notifications?branch=nonsense", HttpStatusCode.BadRequest)]
    public async Task Bad_report_input_is_refused(string path, HttpStatusCode expected)
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);
        auditor.AcceptHtml();

        using var response = await auditor.GetAsync(path);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(DemoAccounts.Clerk, false, false, false)]
    [InlineData(DemoAccounts.Manager, true, false, false)]
    [InlineData(DemoAccounts.Auditor, true, false, true)]
    [InlineData(DemoAccounts.Administrator, true, true, true)]
    public async Task Navigation_links_only_the_pages_of_the_role(
        string email,
        bool seesReports,
        bool seesAdminPages,
        bool seesAudit)
    {
        using var browser = await CounterBrowser.SignInAsync(factory, email);

        var page = await browser.ReadPageAsync("/");

        Assert.Contains(Link("/Notifications"), page, StringComparison.Ordinal);
        Assert.Equal(seesReports, page.Contains(Link("/Reports"), StringComparison.Ordinal));
        Assert.Equal(seesAdminPages, page.Contains(Link("/Staff"), StringComparison.Ordinal));
        Assert.Equal(seesAdminPages, page.Contains(Link("/Branches"), StringComparison.Ordinal));
        Assert.Equal(seesAudit, page.Contains(Link("/Audit"), StringComparison.Ordinal));
    }

    private static string Link(string path) => $"href=\"{path}\"";
}
