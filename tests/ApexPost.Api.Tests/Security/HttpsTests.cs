using System.Net;
using ApexPost.Api.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApexPost.Api.Tests.Security;

/// <summary>
/// Outside Development an HTTPS answer carries Strict-Transport-Security; in Development it does not, so a
/// developer's localhost is never pinned. Plain HTTP is redirected to HTTPS in every environment once the HTTPS
/// port is known.
/// </summary>
public sealed class HttpsTests(ProductionApiFactory production, ApiFactory development)
    : IClassFixture<ProductionApiFactory>, IClassFixture<ApiFactory>
{
    private const string PublicHost = "https://api.apexpost.example";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The Production host has no migrated database, so these requests stop at the 401 before any query; HSTS is
    // added by middleware that runs before authentication, so every HTTPS answer carries it.
    [Fact]
    public async Task Production_https_answer_carries_hsts_for_a_year_with_subdomains()
    {
        using var client = production.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(PublicHost),
        });
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.GetAsync(ApiClient.V2Shipments, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var hsts = Assert.Single(response.Headers.GetValues("Strict-Transport-Security"));
        Assert.Contains("max-age=31536000", hsts, StringComparison.Ordinal);
        Assert.Contains("includeSubDomains", hsts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Development_sends_no_hsts()
    {
        using var client = development.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(PublicHost),
        });

        var response = await client.GetAsync(ApiClient.V2Shipments, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Plain_http_is_redirected_to_https(bool inProduction)
    {
        ApiFactory host = inProduction ? production : development;
        await using var withHttpsPort = host.WithWebHostBuilder(builder => builder.UseSetting("https_port", "443"));
        using var client = withHttpsPort.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(ApiClient.V2Shipments, Ct);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location?.Scheme);
    }
}
