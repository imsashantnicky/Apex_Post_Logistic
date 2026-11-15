using System.Security.Claims;
using System.Text.Encodings.Web;
using ApexPost.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Specs.Support;

/// <summary>
/// The test authentication scheme of the specs (the same idea as <c>TestAuthHandler</c> in ApexPost.Api.Tests): a
/// request with the <see cref="RolesHeader"/> header becomes a principal with those roles and the claim names a real
/// token carries (<see cref="ApiClaims"/>). Without the header the request is anonymous and the API answers 401.
/// </summary>
public sealed class SpecAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The name of the scheme.</summary>
    public const string SchemeName = "Spec";

    /// <summary>The request header that lists the caller's roles, separated by commas.</summary>
    public const string RolesHeader = "X-Spec-Roles";

    /// <summary>The fictional user every principal of this scheme stands for.</summary>
    public const string Subject = "dispatcher-spec";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var roles = header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<Claim> claims = [new(ApiClaims.Subject, Subject)];
        claims.AddRange(roles.Select(role => new Claim(ApiClaims.Role, role)));
        var identity = new ClaimsIdentity(claims, SchemeName, ApiClaims.Subject, ApiClaims.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
