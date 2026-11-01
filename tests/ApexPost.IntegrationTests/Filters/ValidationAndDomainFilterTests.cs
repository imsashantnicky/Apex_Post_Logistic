using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Domain.Transactions;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;

namespace ApexPost.IntegrationTests.Filters;

public sealed class ValidationAndDomainFilterTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string BadRecordPath = "/Transactions/Details/not-a-guid";

    [Fact]
    public async Task Json_caller_gets_validation_problem_details()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var response = await clerk.GetAsync(BadRecordPath);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(problem.GetProperty("errors").TryGetProperty("id", out _));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task Browser_link_with_bad_input_shows_the_400_page()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        clerk.AcceptHtml();

        using var response = await clerk.GetAsync(BadRecordPath);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("400 – Bad Request", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_form_returns_to_its_page_with_a_message_and_is_audited_as_400()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var response = await clerk.PostAsync(
            counter.Path,
            $"/Transactions/AddRetail/{counter.Id}",
            new Dictionary<string, string> { ["Sku"] = "PKG-0001", ["Quantity"] = "many" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(counter.Path, response.Headers.Location?.OriginalString);
        Assert.Contains("One or more fields are not valid.", await counter.PageAsync(), StringComparison.Ordinal);
        var entry = await AuditTrail.EntryForAsync(factory, response, AuditEvents.RetailItemAdded);
        Assert.Equal(400, entry.StatusCode);
    }

    [Fact]
    public async Task Sign_in_keeps_its_generic_message_for_a_blank_form()
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.PostAsync("/Account/Login", "/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("The email or password is not correct.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Broken_business_rule_redirects_with_its_message_and_is_audited_as_400()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var response = await counter.AddParcelAsync(weightKg: 31m);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("alert-bad", await counter.PageAsync(), StringComparison.Ordinal);
        var entry = await AuditTrail.EntryForAsync(factory, response, AuditEvents.ShipmentAdded);
        Assert.Equal(400, entry.StatusCode);
    }

    [Fact]
    public async Task Forbidden_service_call_is_audited_on_the_action_and_as_access_denied()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var otherManager = await CounterBrowser.SignInAsync(factory, DemoAccounts.OtherBranchManager);
        var counter = await Counter.OpenAsync(clerk);

        using var response = await otherManager.PostAsync(
            Counter.TransactionsPath,
            $"/Transactions/Void/{counter.Id}",
            new Dictionary<string, string> { ["Reason"] = "Not my branch." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var attempt = await AuditTrail.EntryForAsync(factory, response, AuditEvents.Void);
        var denied = await AuditTrail.EntryForAsync(factory, response, AuditEvents.AccessDenied);
        Assert.Equal(403, attempt.StatusCode);
        Assert.Equal(attempt.CorrelationId, denied.CorrelationId);
        Assert.Equal(TransactionStatus.Open.ToString(), await StatusShownAsync(clerk, counter));
    }

    private static async Task<string> StatusShownAsync(CounterBrowser browser, Counter counter)
    {
        var page = await browser.ReadPageAsync(counter.Path);
        return page.Contains("pill-open", StringComparison.Ordinal)
            ? TransactionStatus.Open.ToString()
            : "Changed";
    }
}
