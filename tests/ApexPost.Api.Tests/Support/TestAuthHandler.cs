using System.Security.Claims;
using System.Text.Encodings.Web;
using ApexPost.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// A test authentication scheme that replaces JWT validation in <see cref="TestAuthApiFactory"/>. A request carrying
/// the <see cref="RolesHeader"/> header becomes a principal with those roles; a Courier principal also gets the
/// <c>courierId</c> claim <see cref="CourierId"/>, the same claim names a real token carries (<see cref="ApiClaims"/>).
/// A request without the header is not authenticated, so the API answers 401 exactly as for a missing token.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The name of the test scheme.</summary>
    public const string SchemeName = "Test";

    /// <summary>The request header that lists the roles of the test principal, separated by commas.</summary>
    public const string RolesHeader = "X-Test-Roles";

    /// <summary>The fictional courier every Courier principal of the test scheme acts for.</summary>
    public const string CourierId = "CR-101";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var roles = header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var isCourier = roles.Contains(ApiRoles.Courier, StringComparer.Ordinal);
        List<Claim> claims = [new(ApiClaims.Subject, isCourier ? CourierId : "admin-test")];
        claims.AddRange(roles.Select(role => new Claim(ApiClaims.Role, role)));
        if (isCourier)
        {
            claims.Add(new Claim(ApiClaims.CourierId, CourierId));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, ApiClaims.Subject, ApiClaims.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
