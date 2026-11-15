using System.Net;
using System.Security.Cryptography;
using ApexPost.Api.Security;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Security;

/// <summary>
/// Every endpoint needs a valid bearer token. A missing, expired, badly signed or foreign token is answered with
/// 401 ProblemDetails (WWW-Authenticate: Bearer); a valid token of any role may read, in v1 and v2.
/// </summary>
public sealed class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Seeded = "APX-20000001";

    public static TheoryData<string> ReadUrls =>
    [
        $"{ApiClient.Shipments}/{Seeded}",
        $"{ApiClient.V1Shipments}/{Seeded}",
        $"{ApiClient.V2Shipments}/{Seeded}",
        ApiClient.V2Shipments,
        $"{ApiClient.V1Shipments}/{Seeded}/scans",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(ReadUrls))]
    public async Task Missing_token_returns_401_problem_details_with_a_bearer_challenge(string url)
    {
        using var client = factory.ClientWith(token: null);

        var response = await client.GetAsync(url, Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal(BearerProblemEvents.MissingTokenDetail, problem.Detail);
        Assert.NotNull(problem.TraceId);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task Expired_token_returns_401_with_the_expired_detail()
    {
        var token = TestTokens.Create([ApiRoles.Courier], expiresIn: TimeSpan.FromMinutes(-10));
        using var client = factory.ClientWith(token);

        var response = await client.GetAsync($"{ApiClient.V2Shipments}/{Seeded}", Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal(BearerProblemEvents.ExpiredTokenDetail, problem.Detail);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Token_signed_with_another_key_returns_401()
    {
        var otherKey = RandomNumberGenerator.GetBytes(32);
        var token = TestTokens.Create([ApiRoles.Courier, ApiRoles.Admin], signingKey: otherKey);
        using var client = factory.ClientWith(token);

        var response = await client.GetAsync($"{ApiClient.V1Shipments}/{Seeded}", Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal(BearerProblemEvents.InvalidTokenDetail, problem.Detail);
    }

    [Theory]
    [InlineData("https://someone-else.example", TestTokens.Audience)]
    [InlineData(TestTokens.Issuer, "another-api")]
    public async Task Token_of_another_issuer_or_audience_returns_401(string issuer, string audience)
    {
        var token = TestTokens.Create([ApiRoles.Courier], issuer: issuer, audience: audience);
        using var client = factory.ClientWith(token);

        var response = await client.GetAsync($"{ApiClient.V2Shipments}/{Seeded}", Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal(BearerProblemEvents.InvalidTokenDetail, problem.Detail);
    }

    [Fact]
    public async Task Malformed_token_returns_401_and_is_not_echoed()
    {
        const string garbage = "not.a.jwt-token-value";
        using var client = factory.ClientWith(garbage);

        var response = await client.GetAsync($"{ApiClient.V2Shipments}/{Seeded}", Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(garbage, problem.Root.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ApiRoles.Courier)]
    [InlineData(ApiRoles.Admin)]
    public async Task Any_authenticated_role_may_read_in_v1_and_v2(string role)
    {
        using var client = factory.ClientWith(TestTokens.Create([role]));

        var v1 = await client.GetAsync($"{ApiClient.V1Shipments}/{Seeded}", Ct);
        var v2 = await client.GetAsync(ApiClient.V2Shipments, Ct);
        var scans = await client.GetAsync($"{ApiClient.V2Shipments}/{Seeded}/scans", Ct);

        Assert.Equal(
            (HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK),
            (v1.StatusCode, v2.StatusCode, scans.StatusCode));
    }
}
