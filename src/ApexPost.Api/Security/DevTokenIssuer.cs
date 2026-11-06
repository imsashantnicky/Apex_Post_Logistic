using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Security;

/// <summary>
/// Signs Courier tokens for /dev/token with the issuer, audience and signing key the bearer handler validates
/// against (the key that dotnet user-jwts keeps in user-secrets), so a dev token is checked like any other.
/// </summary>
public sealed class DevTokenIssuer(IOptionsMonitor<JwtBearerOptions> bearerOptions, TimeProvider time)
{
    /// <summary>How long a dev token is accepted.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>A Courier token for the courier; null when no issuer, audience or signing key is configured.</summary>
    public DevTokenResponse? Issue(string courierId)
    {
        var validation = bearerOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
        var issuer = validation.ValidIssuers?.FirstOrDefault() ?? validation.ValidIssuer;
        var audience = validation.ValidAudiences?.FirstOrDefault() ?? validation.ValidAudience;
        var key = validation.IssuerSigningKeys?.FirstOrDefault() ?? validation.IssuerSigningKey;
        if (issuer is null || audience is null || key is null)
        {
            return null;
        }

        var now = time.GetUtcNow().UtcDateTime;
        var expires = now + Lifetime;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Claims = new Dictionary<string, object>
            {
                [ApiClaims.Subject] = courierId,
                [ApiClaims.Role] = ApiRoles.Courier,
                [ApiClaims.CourierId] = courierId,
            },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
        return new DevTokenResponse(token, JwtBearerDefaults.AuthenticationScheme, expires, courierId);
    }
}
