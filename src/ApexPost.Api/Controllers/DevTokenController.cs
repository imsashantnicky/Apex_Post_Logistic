using ApexPost.Api.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers;

/// <summary>
/// POST /dev/token: a sign-in for the fictional couriers, so the MAUI app and the .http file get a Courier token
/// without an identity provider. Outside Development this controller is not registered at all
/// (<see cref="DevelopmentOnlyControllers"/>), so the route answers 404. It is left out of the OpenAPI documents.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[ApiExplorerSettings(IgnoreApi = true)]
[AllowAnonymous]
[Route(Route)]
[ProducesJson]
public sealed class DevTokenController(DevTokenIssuer issuer) : ControllerBase
{
    /// <summary>The route of the endpoint.</summary>
    public const string Route = "dev/token";

    /// <summary>The detail of a 500 when dotnet user-jwts has not created a signing key yet.</summary>
    public const string NoSigningKeyDetail =
        "No JWT signing key is configured. Run dotnet user-jwts create --project src/ApexPost.Api once.";

    /// <summary>
    /// 200 with a one-hour Courier token for CR-101 or CR-102; 400 for a malformed courier id; 401 for any other
    /// courier; 500 when no signing key is configured.
    /// </summary>
    /// <param name="request">The courier to sign in.</param>
    [HttpPost]
    [Consumes(ApiRoutes.Json)]
    public ActionResult<DevTokenResponse> Issue(DevTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var courierId = request.CourierId ?? string.Empty;
        if (!DevelopmentCouriers.All.Contains(courierId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: $"Unknown courier. Use a fictional courier: {string.Join(", ", DevelopmentCouriers.All)}.");
        }

        return issuer.Issue(courierId) is { } token
            ? Ok(token)
            : Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Server error",
                detail: NoSigningKeyDetail);
    }
}
