using System.Globalization;
using System.Net;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;

namespace ApexPost.IntegrationTests.Filters;

public sealed class AdminAuditTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Secret = "Audit-test-secret-password";
    private const string StaffPath = "/Staff";

    [Fact]
    public async Task Created_staff_member_is_named_in_the_entry_and_the_password_nowhere()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var created = await admin.PostAsync("/Staff/Create", "/Staff/Create", new Dictionary<string, string>
        {
            ["Name"] = "Nia Brooks",
            ["Email"] = "audited@apexpost.test",
            ["Role"] = "Auditor",
            ["HomeBranch"] = "BR001",
            ["Password"] = Secret,
        });

        var entry = await AuditTrail.EntryForAsync(factory, created, AuditEvents.StaffCreated);
        var member = await factory.StaffAsync("audited@apexpost.test");
        Assert.Equal(member.Id.ToString(), entry.RouteValues["id"]);
        Assert.Equal("Administrator", entry.Role?.ToString());
        Assert.DoesNotContain(Secret, await AuditTrail.RawRowsAsync(factory), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Staff_changes_are_audited_with_the_reason_but_no_password()
    {
        const string email = "clerk1.br004@apexpost.test";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        var member = await factory.StaffAsync(email);
        var id = member.Id;

        using var saved = await admin.PostAsync(StaffPath, $"/Staff/Edit/{id}", new Dictionary<string, string>
        {
            ["version"] = Text(member.Version),
            ["Name"] = "Renamed Clerk",
            ["Email"] = member.Email,
            ["Role"] = member.Role.ToString(),
            ["HomeBranch"] = member.HomeBranch.Value,
        });
        using var password = await admin.PostAsync(
            StaffPath,
            $"/Staff/SetPassword/{id}",
            new Dictionary<string, string> { ["Password"] = Secret });
        using var deactivated = await admin.PostAsync(
            StaffPath,
            $"/Staff/Deactivate/{id}",
            new Dictionary<string, string>
            {
                ["version"] = Text(member.Version + 1),
                ["reason"] = "Left the company.",
            });
        using var activated = await admin.PostAsync(
            StaffPath,
            $"/Staff/Activate/{id}",
            new Dictionary<string, string> { ["version"] = Text(member.Version + 2) });

        await AuditTrail.EntryForAsync(factory, saved, AuditEvents.StaffUpdated);
        await AuditTrail.EntryForAsync(factory, password, AuditEvents.PasswordChanged);
        await AuditTrail.EntryForAsync(factory, activated, AuditEvents.StaffActivated);
        var entry = await AuditTrail.EntryForAsync(factory, deactivated, AuditEvents.StaffDeactivated);
        Assert.Equal("Left the company.", entry.RouteValues["reason"]);
        Assert.Equal(id.ToString(), entry.RouteValues["id"]);
        Assert.DoesNotContain(Secret, await AuditTrail.RawRowsAsync(factory), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refused_change_is_audited_with_its_real_status()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        var member = await factory.StaffAsync(DemoAccounts.SecondClerk);

        using var response = await admin.PostAsync(
            StaffPath,
            $"/Staff/Deactivate/{member.Id}",
            new Dictionary<string, string> { ["version"] = Text(member.Version), ["reason"] = " " });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var entry = await AuditTrail.EntryForAsync(factory, response, AuditEvents.StaffDeactivated);
        Assert.Equal(StatusCodes.BadRequest, entry.StatusCode);
    }

    [Fact]
    public async Task Branch_changes_are_audited_with_the_branch()
    {
        const string code = "BR005";
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var closed = await PostAsync(admin, "Close", code);
        using var reopened = await PostAsync(admin, "Reopen", code);
        using var added = await PostAsync(admin, "AddCounter", code);
        var branch = await factory.BranchAsync(code);
        using var removed = await admin.PostAsync(
            StaffPath,
            $"/Branches/RemoveCounter/{code}",
            new Dictionary<string, string>
            {
                ["counterId"] = branch.Counters[^1].Id.ToString(),
                ["version"] = Text(branch.Version),
            });

        var close = await AuditTrail.EntryForAsync(factory, closed, AuditEvents.BranchClosed);
        Assert.Equal(code, close.RouteValues["id"]);
        await AuditTrail.EntryForAsync(factory, reopened, AuditEvents.BranchReopened);
        await AuditTrail.EntryForAsync(factory, added, AuditEvents.CounterAdded);
        var entry = await AuditTrail.EntryForAsync(factory, removed, AuditEvents.CounterRemoved);
        Assert.Equal(branch.Counters[^1].Id.ToString(), entry.RouteValues["counterId"]);
    }

    [Fact]
    public async Task Denied_admin_request_is_audited()
    {
        using var clerk = await AdminBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var response = await clerk.PostAsync("/", "/Branches/Close/BR001");

        var entry = await AuditTrail.EntryForAsync(factory, response, AuditEvents.AccessDenied);
        Assert.Equal(("Branches", "Close"), (entry.Controller, entry.Action));
        Assert.Equal("BR001", entry.RouteValues["id"]);
    }

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    private async Task<HttpResponseMessage> PostAsync(CounterBrowser admin, string action, string code)
    {
        var branch = await factory.BranchAsync(code);
        return await admin.PostAsync(
            StaffPath,
            $"/Branches/{action}/{code}",
            new Dictionary<string, string> { ["version"] = Text(branch.Version) });
    }

    private static class StatusCodes
    {
        public const int BadRequest = 400;
    }
}
