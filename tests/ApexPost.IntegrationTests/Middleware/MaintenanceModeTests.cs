using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Middleware;

public sealed class MaintenanceModeTests(ApexFactory factory) : IClassFixture<ApexFactory>, IDisposable
{
    public void Dispose() => PipelineOrderTests.SetMaintenance(factory, enabled: false);

    [Fact]
    public async Task Maintenance_answers_503_with_retry_after_and_health_stays_available()
    {
        using var client = PartnerClient.Create(factory);
        PipelineOrderTests.SetMaintenance(factory, enabled: true);

        using var page = await GetAsync(client, "/Account/Login");
        using var health = await GetAsync(client, "/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, page.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(120), page.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", page.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Browsers_see_the_error_page_during_maintenance()
    {
        using var browser = new CounterBrowser(factory);
        browser.AcceptHtml();
        PipelineOrderTests.SetMaintenance(factory, enabled: true);

        using var response = await browser.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(120), response.Headers.RetryAfter?.Delta);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("503 – Service Unavailable", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Turning_maintenance_off_needs_no_restart()
    {
        using var client = PartnerClient.Create(factory);
        PipelineOrderTests.SetMaintenance(factory, enabled: true);
        PipelineOrderTests.SetMaintenance(factory, enabled: false);

        using var page = await GetAsync(client, "/Account/Login");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path) =>
        client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
}
