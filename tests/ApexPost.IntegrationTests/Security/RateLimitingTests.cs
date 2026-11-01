using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Security;

public sealed class RateLimitingTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string LoginPath = "/Account/Login";
    private const int LoginLimit = 5;
    private const int PartnerLimit = 30;

    [Fact]
    public async Task Login_returns_429_after_limit()
    {
        using var browser = new CounterBrowser(factory);

        for (var attempt = 0; attempt < LoginLimit; attempt++)
        {
            using var allowed = await AttemptLoginAsync(browser);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await AttemptLoginAsync(browser);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        AssertRetryAfter(rejected);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_limit_is_per_address_and_only_for_the_post()
    {
        using var limited = new CounterBrowser(factory);
        using var other = new CounterBrowser(factory);
        for (var attempt = 0; attempt <= LoginLimit; attempt++)
        {
            using var response = await AttemptLoginAsync(limited);
        }

        using var page = await limited.GetAsync(LoginPath);
        using var otherAttempt = await AttemptLoginAsync(other);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherAttempt.StatusCode);
    }

    [Fact]
    public async Task Browser_sees_the_error_page_with_retry_after()
    {
        using var browser = new CounterBrowser(factory);
        browser.AcceptHtml();
        for (var attempt = 0; attempt < LoginLimit; attempt++)
        {
            using var allowed = await AttemptLoginAsync(browser);
        }

        using var rejected = await AttemptLoginAsync(browser);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        AssertRetryAfter(rejected);
        var page = await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("429 – Too Many Requests", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partner_returns_429_after_limit_and_other_partners_continue()
    {
        using var burst = PartnerClient.Create(factory, ApexFactory.BurstPartnerKey);
        using var other = PartnerClient.Create(factory);
        for (var call = 0; call < PartnerLimit; call++)
        {
            using var allowed = await GetParcelAsync(burst);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await GetParcelAsync(burst);
        using var otherPartner = await GetParcelAsync(other);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        AssertRetryAfter(rejected);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, otherPartner.StatusCode);
    }

    private static Task<HttpResponseMessage> AttemptLoginAsync(CounterBrowser browser) =>
        browser.PostAsync(LoginPath, LoginPath, CounterBrowser.Login("nobody@apexpost.test", "not-the-password"));

    private static Task<HttpResponseMessage> GetParcelAsync(HttpClient client) =>
        client.GetAsync(new Uri(PartnerClient.Parcel, UriKind.Relative), TestContext.Current.CancellationToken);

    private static void AssertRetryAfter(HttpResponseMessage response)
    {
        var wait = response.Headers.RetryAfter?.Delta;
        Assert.NotNull(wait);
        Assert.InRange(wait.Value, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
    }
}
