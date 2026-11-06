using System.Net;
using System.Text.Json;
using ApexPost.Api.Security;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Security;

/// <summary>
/// GET /api/v2/partner/shipments/{trackingNumber} needs a partner key in X-Api-Key: 200 with a known key, 401
/// ProblemDetails (WWW-Authenticate: ApiKey) without one or with an unknown one. A user token does not open it.
/// </summary>
public sealed class PartnerApiKeyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string InTransitShipment = "APX-20000005";
    private const string PartnerShipments = "/api/v2/partner/shipments";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string PartnerUrl(string trackingNumber) => $"{PartnerShipments}/{trackingNumber}";

    private HttpClient PartnerClient(string? key)
    {
        var client = factory.ClientWith(token: null);
        if (key is not null)
        {
            client.DefaultRequestHeaders.Add(PartnerApiKeyFilter.HeaderName, key);
        }

        return client;
    }

    [Theory]
    [InlineData(TestPartnerKeys.Key)]
    [InlineData(TestPartnerKeys.OtherKey)]
    public async Task Valid_key_returns_200_with_status_city_and_latest_scan_only(string key)
    {
        using var client = PartnerClient(key);

        var response = await client.GetAsync(PartnerUrl(InTransitShipment), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = body.RootElement;
        Assert.Equal(InTransitShipment, root.GetProperty("trackingNumber").GetString());
        Assert.Equal("InTransit", root.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("destinationCity").GetString()));
        Assert.True(root.TryGetProperty("latestScan", out _));
        Assert.Equal(
            ["destinationCity", "latestScan", "status", "trackingNumber"],
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Missing_key_returns_401_problem_details_with_an_ApiKey_challenge()
    {
        using var client = PartnerClient(key: null);

        var response = await client.GetAsync(PartnerUrl(InTransitShipment), Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal(PartnerApiKeyFilter.MissingKeyDetail, problem.Detail);
        Assert.NotNull(problem.TraceId);
        Assert.Equal(PartnerApiKeyFilter.SchemeName, Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Theory]
    [InlineData("not-a-partner-key")]
    [InlineData("TEST-PARTNER-KEY-1C6F0B6E")]
    [InlineData(TestPartnerKeys.Key + "x")]
    [InlineData("test-partner-key-1c6f0b6")]
    public async Task Invalid_key_returns_401_and_is_not_echoed(string key)
    {
        using var client = PartnerClient(key);

        var response = await client.GetAsync(PartnerUrl(InTransitShipment), Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal(PartnerApiKeyFilter.InvalidKeyDetail, problem.Detail);
        Assert.DoesNotContain(key, problem.Root.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_user_token_does_not_replace_the_partner_key()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(PartnerUrl(InTransitShipment), Ct);

        await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unknown_shipment_with_a_valid_key_returns_404()
    {
        using var client = PartnerClient(TestPartnerKeys.Key);

        var response = await client.GetAsync(PartnerUrl("APX-29999999"), Ct);

        await Problem.ReadAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_partner_endpoint_exists_in_v2_only()
    {
        using var client = PartnerClient(TestPartnerKeys.Key);

        var response = await client.GetAsync($"/api/v1/partner/shipments/{InTransitShipment}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
