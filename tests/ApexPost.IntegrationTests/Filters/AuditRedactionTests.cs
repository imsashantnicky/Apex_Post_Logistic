using System.Net;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;

namespace ApexPost.IntegrationTests.Filters;

public sealed class AuditRedactionTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string LoginPath = "/Account/Login";
    private const string DocumentNumber = "ZX9876543";

    [Fact]
    public async Task Document_number_of_a_verification_never_reaches_the_audit_file()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var verified = await counter.VerifyAsync(DocumentNumber);

        await AuditTrail.EntryForAsync(factory, verified, AuditEvents.IdentityVerification);
        Assert.DoesNotContain(DocumentNumber, await AuditTrail.RawFileAsync(factory), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Password_never_reaches_the_audit_file()
    {
        const string password = "typed-a-wrong-password";
        using var browser = new CounterBrowser(factory);

        using var failed = await browser.PostAsync(
            LoginPath,
            $"{LoginPath}?password={password}",
            CounterBrowser.Login(DemoAccounts.Clerk, password));

        await AuditTrail.EntryForAsync(factory, failed, AuditEvents.Login);
        Assert.DoesNotContain(password, await AuditTrail.RawFileAsync(factory), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Document_number_typed_into_a_url_is_masked()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var response = await clerk.PostAsync(
            "/",
            $"/Transactions/VerifyIdentity/{DocumentNumber}",
            new Dictionary<string, string> { ["DocumentType"] = "Passport", ["DocumentNumber"] = DocumentNumber });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var entry = await AuditTrail.EntryForAsync(factory, response, AuditEvents.IdentityVerification);
        Assert.Equal("****6543", entry.RouteValues["id"]);
        Assert.DoesNotContain(DocumentNumber, await AuditTrail.RawFileAsync(factory), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sensitive_text_in_a_scalar_argument_is_removed()
    {
        const string url = "/transfer/4111-1111-1111-1111/AB1234567";
        using var browser = new CounterBrowser(factory);
        var encoded = WebUtility.UrlEncode(url);

        using var failed = await browser.PostAsync(
            LoginPath,
            $"{LoginPath}?returnUrl={encoded}",
            CounterBrowser.Login(DemoAccounts.Clerk, "wrong-password-for-tests"));

        var entry = await AuditTrail.EntryForAsync(factory, failed, AuditEvents.Login);
        Assert.Equal("/transfer/[redacted]/****4567", entry.RouteValues["returnUrl"]);
    }
}
