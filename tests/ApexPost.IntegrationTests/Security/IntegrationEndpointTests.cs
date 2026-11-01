using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.IntegrationTests.Support;
using Microsoft.Extensions.Logging;

namespace ApexPost.IntegrationTests.Security;

public sealed class IntegrationEndpointTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string OriginHeader = "Origin";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    [Fact]
    public async Task Partner_with_a_valid_key_gets_the_parcel_status()
    {
        using var client = PartnerClient.Create(factory);

        using var response = await GetAsync(client, PartnerClient.Parcel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("APX123456", status.GetProperty("trackingNumber").GetString());
        Assert.Equal("In transit", status.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Integration_returns_401_without_api_key()
    {
        using var client = PartnerClient.Create(factory, apiKey: null);

        using var response = await GetAsync(client, PartnerClient.Parcel);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData(ApexFactory.PartnerKey + "x")]
    public async Task Integration_returns_401_with_a_wrong_key(string key)
    {
        using var client = PartnerClient.Create(factory, key);

        using var response = await GetAsync(client, PartnerClient.Parcel);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Integration_returns_403_from_blocked_ip()
    {
        using var client = PartnerClient.Create(factory, address: PartnerClient.BlockedAddress);

        using var response = await GetAsync(client, PartnerClient.Parcel);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Partner_gets_json_errors_even_when_it_asks_for_html()
    {
        using var client = PartnerClient.Create(factory, apiKey: null);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        using var response = await GetAsync(client, PartnerClient.Parcel);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Failing_courier_answers_502()
    {
        using var client = PartnerClient.Create(factory);

        using var response = await GetAsync(client, "/integration/parcel-status/FAIL1");

        await AssertProblemAsync(response, HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Slow_courier_answers_504()
    {
        using var client = PartnerClient.Create(factory);

        using var response = await GetAsync(client, "/integration/parcel-status/SLOW1");

        await AssertProblemAsync(response, HttpStatusCode.GatewayTimeout);
    }

    [Fact]
    public async Task Too_long_tracking_number_answers_400()
    {
        using var client = PartnerClient.Create(factory);

        using var response = await GetAsync(client, $"/integration/parcel-status/{new string('A', 33)}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Default_route_cannot_reach_the_endpoint_without_a_key_or_a_number()
    {
        using var anonymous = PartnerClient.Create(factory, apiKey: null);
        using var partner = PartnerClient.Create(factory);

        using var withoutKey = await GetAsync(anonymous, "/Integration/ParcelStatus/APX1");
        using var withoutNumber = await GetAsync(partner, "/Integration/ParcelStatus");

        Assert.Equal(HttpStatusCode.Unauthorized, withoutKey.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutNumber.StatusCode);
    }

    [Fact]
    public async Task Client_that_hangs_up_is_logged_as_499_and_not_as_an_error()
    {
        using var client = PartnerClient.Create(factory);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync(new Uri("/integration/parcel-status/SLOW2", UriKind.Relative), cancellation.Token));

        var entry = await factory.Logs.WaitForAsync(log =>
            log.Message.Contains("SLOW2 answered", StringComparison.Ordinal));
        Assert.Contains("answered 499", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.Logs.Entries, log =>
            log.Level >= LogLevel.Error && log.Scopes.SequenceEqual(entry.Scopes));
    }

    [Fact]
    public async Task Cors_allows_the_partner_origin()
    {
        using var client = PartnerClient.Create(factory);

        using var response = await SendWithOriginAsync(client, ApexFactory.PartnerOrigin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ApexFactory.PartnerOrigin, Assert.Single(response.Headers.GetValues(AllowOriginHeader)));
    }

    [Fact]
    public async Task Cors_ignores_any_other_origin()
    {
        using var client = PartnerClient.Create(factory);

        using var response = await SendWithOriginAsync(client, "https://evil.example");

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    [Fact]
    public async Task Cors_preflight_only_allows_get()
    {
        using var client = PartnerClient.Create(factory);
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri(PartnerClient.Parcel, UriKind.Relative));
        request.Headers.Add(OriginHeader, ApexFactory.PartnerOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path) =>
        client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

    private static async Task<HttpResponseMessage> SendWithOriginAsync(HttpClient client, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(PartnerClient.Parcel, UriKind.Relative));
        request.Headers.Add(OriginHeader, origin);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("correlationId").GetString()));
    }
}
