using System.Net.Http.Headers;
using System.Security.Cryptography;
using ApexPost.Api.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// JWTs for the tests. <see cref="ApiFactory"/> configures the host with <see cref="Issuer"/>, <see cref="Audience"/>
/// and a signing key made for this test run (the same shape dotnet user-jwts writes), so no real key is needed.
/// </summary>
public static class TestTokens
{
    public const string Issuer = "https://auth.tests.apexpost.example";
    public const string Audience = "apexpost-api-tests";
    public const string Courier = "CR-101";
    public const string OtherCourier = "CR-102";

    private const string BearerSection = "Authentication:Schemes:Bearer";

    private static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>The settings the host reads in place of appsettings and the dotnet user-jwts user-secret.</summary>
    public static IReadOnlyDictionary<string, string?> HostConfiguration { get; } = new Dictionary<string, string?>
    {
        [$"{BearerSection}:ValidIssuer"] = Issuer,
        [$"{BearerSection}:ValidAudiences:0"] = Audience,
        [$"{BearerSection}:SigningKeys:0:Id"] = "tests",
        [$"{BearerSection}:SigningKeys:0:Issuer"] = Issuer,
        [$"{BearerSection}:SigningKeys:0:Value"] = Convert.ToBase64String(SigningKey),
        [$"{BearerSection}:SigningKeys:0:Length"] = "32",
    };

    /// <summary>A token with both roles for CR-101; every existing endpoint test sends it.</summary>
    public static string AllRoles() => Create([ApiRoles.Courier, ApiRoles.Admin]);

    /// <summary>A valid token unless a parameter breaks it; expiresIn may be negative for an expired token.</summary>
    public static string Create(
        string[] roles,
        string? courierId = Courier,
        TimeSpan? expiresIn = null,
        byte[]? signingKey = null,
        string issuer = Issuer,
        string audience = Audience)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var expires = DateTime.UtcNow + (expiresIn ?? TimeSpan.FromHours(1));
        var issued = expires.AddHours(-2);
        var claims = new Dictionary<string, object> { [ApiClaims.Subject] = courierId ?? "admin" };
        if (roles.Length > 0)
        {
            claims[ApiClaims.Role] = roles;
        }

        if (courierId is not null)
        {
            claims[ApiClaims.CourierId] = courierId;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = issued,
            NotBefore = issued,
            Expires = expires,
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(signingKey ?? SigningKey),
                SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>A client of the factory that sends this token, or no token at all for null.</summary>
    public static HttpClient ClientWith(this ApiFactory factory, string? token)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
