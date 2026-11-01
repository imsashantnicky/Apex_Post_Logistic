using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;

namespace ApexPost.IntegrationTests.Filters;

public sealed class AuditPageTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string AuditPath = "/Audit";
    private const string ExportPath = "/Audit/Export";

    [Theory]
    [InlineData(DemoAccounts.Auditor)]
    [InlineData(DemoAccounts.Administrator)]
    public async Task Audit_viewers_read_the_log_from_an_allowed_address(string email)
    {
        using var viewer = await AdminBrowserAsync(email);

        var page = await viewer.ReadPageAsync(AuditPath);

        Assert.Contains("Audit log", page, StringComparison.Ordinal);
        Assert.Contains(AuditEvents.Login, page, StringComparison.Ordinal);
        Assert.Contains("Export JSON", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_downloads_readable_json_and_is_audited_itself()
    {
        using var viewer = await AdminBrowserAsync(DemoAccounts.Auditor);

        using var response = await viewer.GetAsync(ExportPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var login = rows.EnumerateArray().First(row => row.GetProperty("event").GetString() == AuditEvents.Login);
        Assert.Equal("Auditor", login.GetProperty("role").GetString());
        await AuditTrail.EntryForAsync(factory, response, AuditEvents.AuditExported);
    }

    [Theory]
    [InlineData(AuditPath)]
    [InlineData(ExportPath)]
    public async Task Clerk_gets_an_audited_403(string path)
    {
        using var clerk = await AdminBrowserAsync(DemoAccounts.Clerk);

        using var response = await clerk.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AuditTrail.EntryForAsync(factory, response, AuditEvents.AccessDenied);
    }

    [Fact]
    public async Task Auditor_on_a_blocked_address_gets_an_audited_403_page()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);
        auditor.AcceptHtml();

        using var response = await auditor.GetAsync(AuditPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("403 – Access denied", page, StringComparison.Ordinal);
        var entry = await AuditTrail.EntryForAsync(factory, response, AuditEvents.AccessDenied);
        Assert.Equal("Audit", entry.Controller);
        Assert.NotNull(entry.StaffId);
    }

    [Fact]
    public async Task Json_caller_on_a_blocked_address_is_audited_too()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);

        using var response = await auditor.GetAsync(ExportPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await AuditTrail.EntryForAsync(factory, response, AuditEvents.AccessDenied);
    }

    private async Task<CounterBrowser> AdminBrowserAsync(string email)
    {
        var browser = await CounterBrowser.SignInAsync(factory, email);
        browser.CallFrom(PartnerClient.AllowedAddress);
        return browser;
    }
}
