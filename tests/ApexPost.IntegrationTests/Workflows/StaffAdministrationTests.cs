using System.Globalization;
using System.Net;
using ApexPost.Domain.Personnel;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Workflows;

public sealed class StaffAdministrationTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string StaffPath = "/Staff";
    private const string NewPassword = "A-new-password-for-tests";

    [Fact]
    public async Task List_shows_staff_of_all_branches_with_their_status()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        var page = await admin.ReadPageAsync(StaffPath);

        Assert.Contains(DemoAccounts.Clerk, page, StringComparison.Ordinal);
        Assert.Contains(DemoAccounts.OtherBranchManager, page, StringComparison.Ordinal);
        Assert.Contains("Branch Manager", page, StringComparison.Ordinal);
        Assert.Contains("pill-inactive", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_staff_member_can_sign_in_at_once()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        var details = Details("new.clerk@apexpost.test");

        using var response = await admin.PostAsync("/Staff/Create", "/Staff/Create", details);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var member = await factory.StaffAsync("new.clerk@apexpost.test");
        Assert.Equal($"/Staff/Edit/{member.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal((StaffRole.CounterClerk, "BR003"), (member.Role, member.HomeBranch.Value));
        Assert.True(await CanSignInAsync("new.clerk@apexpost.test", NewPassword));
    }

    [Fact]
    public async Task Taken_email_and_short_password_return_to_the_form_with_a_message()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var taken = await admin.PostAsync("/Staff/Create", "/Staff/Create", Details(DemoAccounts.Clerk));
        var takenPage = await admin.ReadPageAsync(taken.Headers.Location!.OriginalString);
        using var weak = await admin.PostAsync(
            "/Staff/Create",
            "/Staff/Create",
            new Dictionary<string, string>(Details("weak@apexpost.test")) { ["Password"] = "short" });
        var weakPage = await admin.ReadPageAsync(weak.Headers.Location!.OriginalString);

        Assert.Contains("This email is already in use.", takenPage, StringComparison.Ordinal);
        Assert.Contains("Password must be at least 12 characters.", weakPage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Role_change_applies_to_the_open_session()
    {
        const string email = "clerk2.br003@apexpost.test";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var member = await CounterBrowser.SignInAsync(factory, email);
        using var before = await member.GetAsync("/Reports/DailySales");

        using var saved = await SaveAsync(admin, await factory.StaffAsync(email), role: "BranchManager");
        using var after = await member.GetAsync("/Reports/DailySales");

        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task Move_to_another_branch_changes_what_the_open_session_may_read()
    {
        const string email = "manager.br004@apexpost.test";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var manager = await CounterBrowser.SignInAsync(factory, email);
        var member = await factory.StaffAsync(email);

        using var saved = await SaveAsync(admin, member, branch: "BR005");
        using var oldBranch = await manager.GetAsync("/Reports/LowStock?branch=BR004");
        using var newBranch = await manager.GetAsync("/Reports/LowStock?branch=BR005");

        Assert.Equal(HttpStatusCode.Forbidden, oldBranch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newBranch.StatusCode);
    }

    [Fact]
    public async Task Stale_form_is_refused_and_changes_nothing()
    {
        const string email = "clerk1.br005@apexpost.test";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        var stale = await factory.StaffAsync(email);
        using var first = await SaveAsync(admin, stale, name: "First Edit");

        using var second = await SaveAsync(admin, stale, name: "Second Edit");

        Assert.Equal($"/Staff/Edit/{stale.Id}", second.Headers.Location?.OriginalString);
        var page = await admin.ReadPageAsync(second.Headers.Location!.OriginalString);
        Assert.Contains("changed by someone else", page, StringComparison.Ordinal);
        Assert.Equal("First Edit", (await factory.StaffAsync(email)).Name);
    }

    [Fact]
    public async Task Deactivation_needs_a_reason_then_ends_the_session_and_can_be_undone()
    {
        const string email = "clerk2.br002@apexpost.test";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var member = await CounterBrowser.SignInAsync(factory, email);
        var target = await factory.StaffAsync(email);

        using var noReason = await DeactivateAsync(admin, target, "  ");
        Assert.True((await factory.StaffAsync(email)).IsActive);

        using var deactivated = await DeactivateAsync(admin, target, "Left the company.");
        using var session = await member.GetAsync("/Transactions");

        Assert.Equal(HttpStatusCode.Redirect, session.StatusCode);
        Assert.Contains("/Account/Login", session.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.False(await CanSignInAsync(email, ApexFactory.DemoPassword));

        var current = await factory.StaffAsync(email);
        using var activated = await admin.PostAsync(
            StaffPath,
            $"/Staff/Activate/{current.Id}",
            new Dictionary<string, string> { ["version"] = Text(current.Version) });

        Assert.True(await CanSignInAsync(email, ApexFactory.DemoPassword));
    }

    [Fact]
    public async Task New_password_replaces_the_old_one()
    {
        const string email = "clerk3.br002@apexpost.test";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        var member = await factory.StaffAsync(email);

        using var response = await admin.PostAsync(
            StaffPath,
            $"/Staff/SetPassword/{member.Id}",
            new Dictionary<string, string> { ["Password"] = NewPassword });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await CanSignInAsync(email, ApexFactory.DemoPassword));
        Assert.True(await CanSignInAsync(email, NewPassword));
    }

    [Fact]
    public async Task Administrator_gets_no_deactivate_form_for_the_own_account_and_cannot_post_one()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.SecondAdministrator);
        var own = await factory.StaffAsync(DemoAccounts.SecondAdministrator);

        var page = await admin.ReadPageAsync($"/Staff/Edit/{own.Id}");
        using var response = await DeactivateAsync(admin, own, "Trying anyway.");

        Assert.DoesNotContain("Deactivate", page, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True((await factory.StaffAsync(DemoAccounts.SecondAdministrator)).IsActive);
    }

    [Fact]
    public async Task Administrator_cannot_change_the_own_role()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.SecondAdministrator);
        var own = await factory.StaffAsync(DemoAccounts.SecondAdministrator);

        using var response = await SaveAsync(admin, own, role: "CounterClerk");

        var page = await admin.ReadPageAsync(response.Headers.Location!.OriginalString);
        Assert.Contains("You cannot change your own role.", page, StringComparison.Ordinal);
        Assert.Equal(StaffRole.Administrator, (await factory.StaffAsync(DemoAccounts.SecondAdministrator)).Role);
    }

    [Fact]
    public async Task Unknown_staff_member_is_not_found()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var response = await admin.GetAsync($"/Staff/Edit/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Dictionary<string, string> Details(string email) => new()
    {
        ["Name"] = "Nia Brooks",
        ["Email"] = email,
        ["Role"] = "CounterClerk",
        ["HomeBranch"] = "BR003",
        ["Password"] = NewPassword,
    };

    private static Task<HttpResponseMessage> SaveAsync(
        CounterBrowser admin,
        Staff member,
        string? name = null,
        string? role = null,
        string? branch = null) =>
        admin.PostAsync(StaffPath, $"/Staff/Edit/{member.Id}", new Dictionary<string, string>
        {
            ["version"] = Text(member.Version),
            ["Name"] = name ?? member.Name,
            ["Email"] = member.Email,
            ["Role"] = role ?? member.Role.ToString(),
            ["HomeBranch"] = branch ?? member.HomeBranch.Value,
        });

    private static Task<HttpResponseMessage> DeactivateAsync(CounterBrowser admin, Staff member, string reason) =>
        admin.PostAsync(StaffPath, $"/Staff/Deactivate/{member.Id}", new Dictionary<string, string>
        {
            ["version"] = Text(member.Version),
            ["reason"] = reason,
        });

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    private async Task<bool> CanSignInAsync(string email, string password)
    {
        using var browser = new CounterBrowser(factory);
        using var response = await browser.PostAsync(
            "/Account/Login",
            "/Account/Login",
            CounterBrowser.Login(email, password));
        return response.StatusCode == HttpStatusCode.Redirect;
    }
}
