using System.Net;
using ApexPost.Domain.Personnel;
using ApexPost.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Authentication;

public sealed class SignInTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string LoginPath = "/Account/Login";
    private const string FailureMessage = "The email or password is not correct.";

    [Fact]
    public async Task Unauthenticated_request_redirects_to_login()
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.GetAsync("/Transactions");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(LoginPath, response.Headers.Location!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_in_sets_a_hardened_cookie_and_shows_name_role_and_branch()
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.PostAsync(
            LoginPath,
            LoginPath,
            CounterBrowser.Login(DemoAccounts.Manager, ApexFactory.DemoPassword));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(".ApexPost.Auth=", StringComparison.Ordinal)).ToUpperInvariant();
        Assert.Contains("HTTPONLY", cookie, StringComparison.Ordinal);
        Assert.Contains("SECURE", cookie, StringComparison.Ordinal);
        Assert.Contains("SAMESITE=STRICT", cookie, StringComparison.Ordinal);
        var dashboard = await browser.ReadPageAsync("/");
        Assert.Contains("Branch Manager", dashboard, StringComparison.Ordinal);
        Assert.Contains("Central Counter (BR001)", dashboard, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DemoAccounts.Clerk, "wrong-password-for-tests")]
    [InlineData("nobody@apexpost.test", ApexFactory.DemoPassword)]
    [InlineData(DemoAccounts.InactiveClerk, ApexFactory.DemoPassword)]
    public async Task Failed_sign_in_shows_one_generic_message(string email, string password)
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.PostAsync(LoginPath, LoginPath, CounterBrowser.Login(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(FailureMessage, page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_in_returns_to_a_local_url_but_not_to_another_site()
    {
        using var local = new CounterBrowser(factory);
        using var other = new CounterBrowser(factory);
        var fields = CounterBrowser.Login(DemoAccounts.Clerk, ApexFactory.DemoPassword);

        using var toLocal = await local.PostAsync(LoginPath, $"{LoginPath}?returnUrl=%2FCatalogue", fields);
        using var toOther = await other.PostAsync(
            LoginPath,
            $"{LoginPath}?returnUrl=https%3A%2F%2Fevil.example",
            fields);

        Assert.Equal("/Catalogue", toLocal.Headers.Location!.OriginalString);
        Assert.Equal("/", toOther.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Sign_out_ends_the_session()
    {
        using var browser = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var signOut = await browser.PostAsync("/", "/Account/Logout");
        using var next = await browser.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode);
        Assert.StartsWith(LoginPath, next.Headers.Location!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deactivated_staff_lose_access_on_the_next_request()
    {
        using var browser = await CounterBrowser.SignInAsync(factory, DemoAccounts.SecondClerk);
        await ChangeAsync(DemoAccounts.SecondClerk, member => member.Deactivate());

        using var response = await browser.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(LoginPath, response.Headers.Location!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_changed_role_applies_on_the_next_request()
    {
        const string email = "clerk3.br001@apexpost.test";
        using var browser = await CounterBrowser.SignInAsync(factory, email);
        await ChangeAsync(email, member => member.Role = StaffRole.Auditor);

        using var response = await browser.GetAsync("/Transactions/New");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task ChangeAsync(string email, Action<Staff> change)
    {
        await using var database = factory.OpenDatabase();
        var member = await database.Set<Staff>().SingleAsync(
            candidate => candidate.Email == email,
            CancellationToken.None);
        change(member);
        await database.SaveChangesAsync(CancellationToken.None);
    }
}
