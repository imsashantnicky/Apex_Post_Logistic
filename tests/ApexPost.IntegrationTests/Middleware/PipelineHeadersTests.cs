using System.Net;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Middleware;
using Microsoft.Extensions.Logging;

namespace ApexPost.IntegrationTests.Middleware;

public sealed class PipelineHeadersTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string LoginPath = "/Account/Login";

    [Theory]
    [InlineData(LoginPath)]
    [InlineData("/css/site.css")]
    [InlineData("/no/such/page")]
    public async Task Security_headers_are_present(string path)
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.GetAsync(path);

        var headers = response.Headers;
        var policy = Assert.Single(headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", policy, StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(headers.GetValues("X-Frame-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task Correlation_id_header_is_returned()
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.GetAsync(LoginPath);

        Assert.False(string.IsNullOrEmpty(CorrelationId(response)));
    }

    [Fact]
    public async Task Safe_incoming_correlation_id_is_reused_and_shown_in_the_footer()
    {
        using var client = PartnerClient.Create(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(LoginPath, UriKind.Relative));
        request.Headers.Add(ApexHeaders.CorrelationId, "order-77.a_b");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("order-77.a_b", CorrelationId(response));
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Correlation ID order-77.a_b", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("semi;colon")]
    public async Task Unsafe_incoming_correlation_id_is_replaced(string unsafeId)
    {
        using var client = PartnerClient.Create(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(LoginPath, UriKind.Relative));
        request.Headers.TryAddWithoutValidation(ApexHeaders.CorrelationId, unsafeId);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(unsafeId, CorrelationId(response));
    }

    [Fact]
    public async Task Rejected_requests_are_timed_and_correlated_too()
    {
        using var client = PartnerClient.Create(factory, apiKey: null);

        using var response = await client.GetAsync(
            new Uri(PartnerClient.Parcel, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(CorrelationId(response)));
        Assert.True(response.Headers.Contains(ApexHeaders.ResponseTime));
    }

    [Fact]
    public async Task Request_log_entry_is_structured_and_carries_the_correlation_id()
    {
        using var client = PartnerClient.Create(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(PartnerClient.Parcel, UriKind.Relative));
        request.Headers.Add(ApexHeaders.CorrelationId, "log-entry-check");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var entry = await factory.Logs.WaitForAsync(log =>
            log.Category == typeof(RequestLoggingMiddleware).FullName &&
            log.Scopes.Contains("CorrelationId=log-entry-check"));
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains($"GET {PartnerClient.Parcel} answered 200 in", entry.Message, StringComparison.Ordinal);
    }

    private static string CorrelationId(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues(ApexHeaders.CorrelationId));
}
