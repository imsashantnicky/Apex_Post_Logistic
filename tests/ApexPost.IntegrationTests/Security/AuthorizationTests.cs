using System.Net;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Security;

public sealed class AuthorizationTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Sku = "PKG-0001";

    [Theory]
    [InlineData(ApexPolicies.BranchManagerOnly)]
    [InlineData(ApexPolicies.SameBranchOnly)]
    [InlineData(ApexPolicies.CanTransact)]
    [InlineData(ApexPolicies.CanViewReports)]
    [InlineData(ApexPolicies.AdministratorOnly)]
    [InlineData(ApexPolicies.AuditViewers)]
    public async Task Named_policy_exists(string policy)
    {
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.NotNull(await provider.GetPolicyAsync(policy));
    }

    [Fact]
    public async Task Clerk_gets_403_on_void()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var response = await counter.VoidAsync("Customer changed their mind.");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Clerk_gets_403_on_refund_and_stock_adjustment()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var refund = await counter.RefundAsync("Damaged item.");
        using var adjust = await clerk.GetAsync($"/Catalogue/Adjust?sku={Sku}");

        Assert.Equal(HttpStatusCode.Forbidden, refund.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adjust.StatusCode);
    }

    [Theory]
    [InlineData(DemoAccounts.Auditor)]
    [InlineData(DemoAccounts.Administrator)]
    public async Task Auditors_and_administrators_cannot_open_a_transaction(string email)
    {
        using var browser = await CounterBrowser.SignInAsync(factory, email);

        using var response = await browser.GetAsync("/Transactions/New");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_of_another_branch_get_403_on_a_transaction()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var otherManager = await CounterBrowser.SignInAsync(factory, DemoAccounts.OtherBranchManager);
        using var otherClerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.OtherBranchClerk);
        var counter = await Counter.OpenAsync(clerk);

        using var details = await otherClerk.GetAsync(counter.Path);
        using var voided = await otherManager.PostAsync(
            "/Transactions",
            $"/Transactions/Void/{counter.Id}",
            new Dictionary<string, string> { ["Reason"] = "Not my branch." });

        Assert.Equal(HttpStatusCode.Forbidden, details.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, voided.StatusCode);
    }

    [Theory]
    [InlineData("/Catalogue?branch=BR002")]
    [InlineData("/Transactions?branch=BR002")]
    [InlineData("/?branch=BR002")]
    public async Task Clerk_gets_403_when_naming_another_branch(string path)
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var response = await clerk.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_can_read_any_branch_but_gets_no_counter_forms()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);
        var counter = await Counter.OpenAsync(clerk);

        var catalogue = await auditor.ReadPageAsync("/Catalogue?branch=BR002");
        var screen = await auditor.ReadPageAsync(counter.Path);

        Assert.Contains("BR002 stock", catalogue, StringComparison.Ordinal);
        Assert.DoesNotContain("Take payment", screen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bad_branch_code_is_a_bad_request()
    {
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);

        using var response = await auditor.GetAsync("/Catalogue?branch=nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_transaction_is_not_found()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var response = await clerk.GetAsync($"/Transactions/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var response = await clerk.PostWithoutPageAsync(
            $"/Transactions/Pay/{counter.Id}",
            new Dictionary<string, string> { ["method"] = "Card" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
