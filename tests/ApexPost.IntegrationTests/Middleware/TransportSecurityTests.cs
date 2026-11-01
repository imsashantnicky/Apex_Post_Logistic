using System.Globalization;
using System.Net;
using ApexPost.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApexPost.IntegrationTests.Middleware;

public sealed class TransportSecurityTests(ProductionFactory factory) : IClassFixture<ProductionFactory>
{
    [Fact]
    public async Task Hsts_is_sent_outside_development()
    {
        using var client = ClientFor("https://counter.apexpost.example");

        using var response = await client.GetAsync(LoginPage, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Plain_http_is_redirected_to_https()
    {
        using var client = ClientFor("http://counter.apexpost.example");

        using var response = await client.GetAsync(LoginPage, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location?.Scheme);
        var port = response.Headers.Location?.Port.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(ProductionFactory.HttpsPort, port);
    }

    private static Uri LoginPage => new("/Account/Login", UriKind.Relative);

    private HttpClient ClientFor(string address) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri(address),
    });
}
