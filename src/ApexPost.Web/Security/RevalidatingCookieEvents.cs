using System.Security.Claims;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ApexPost.Web.Security;

/// <summary>
/// Checks the staff record on each request: a deactivated account is signed out at once, and a changed role or
/// home branch replaces the claims of the cookie. Access denied answers 403 instead of redirecting.
/// </summary>
public sealed class RevalidatingCookieEvents(LoginService login) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var staff = context.Principal?.StaffId is { } staffId
            ? await login.RevalidateAsync(staffId, context.HttpContext.RequestAborted)
            : null;
        if (staff is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        else if (!Matches(context.Principal!, staff))
        {
            context.ReplacePrincipal(StaffPrincipal.Create(staff));
            context.ShouldRenew = true;
        }
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static bool Matches(ClaimsPrincipal principal, AuthenticatedStaff staff) =>
        principal.Role == staff.Role && principal.BranchCode == staff.Branch && principal.Identity?.Name == staff.Name;
}
