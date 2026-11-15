using ApexPost.Application.Tracking;

namespace ApexPost.Api.Security;

/// <summary>
/// The courier of the current request, read from the bearer token: the <c>courierId</c> claim of a token with the
/// Courier role. Any other caller is no courier (null), so the tracking service refuses its scan uploads.
/// </summary>
public sealed class ClaimsCurrentCourier(IHttpContextAccessor accessor) : ICurrentCourier
{
    /// <summary>The courierId claim of a Courier token, otherwise null.</summary>
    public string? CourierId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            return user?.IsInRole(ApiRoles.Courier) == true ? user.FindFirst(ApiClaims.CourierId)?.Value : null;
        }
    }
}
