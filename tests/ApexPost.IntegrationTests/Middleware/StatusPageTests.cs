using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Middleware;

public sealed class StatusPageTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    [Fact]
    public async Task Browser_gets_the_403_page_with_trace_and_correlation_ids()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        clerk.AcceptHtml();

        using var response = await clerk.GetAsync("/Catalogue/Adjust?sku=PKG-0001");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("403 – Access denied", page, StringComparison.Ordinal);
        Assert.Contains("Trace ID:", page, StringComparison.Ordinal);
        Assert.Contains("Correlation ID:", page, StringComparison.Ordinal);
        Assert.Contains("Back to the start", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_page_shows_404_to_a_signed_in_browser()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        clerk.AcceptHtml();

        using var response = await clerk.GetAsync("/Dashboard/NoSuchAction");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("404 – Not Found", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_browser_is_sent_to_sign_in_for_a_missing_page()
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.GetAsync("/Dashboard/NoSuchAction");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Json_caller_gets_problem_details_for_an_empty_error_response()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var response = await clerk.GetAsync($"/Transactions/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_rejected_by_antiforgery_reaches_the_error_page()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        clerk.AcceptHtml();

        using var response = await clerk.PostWithoutPageAsync("/Account/Logout", []);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("400 – Bad Request", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(302)]
    [InlineData(600)]
    public async Task Status_page_only_shows_error_codes(int status)
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.GetAsync($"/Error/Status/{status}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
