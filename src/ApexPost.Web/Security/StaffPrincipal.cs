using System.Globalization;
using System.Security.Claims;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ApexPost.Web.Security;

/// <summary>Turns a staff member into the cookie principal with the StaffId, Role and BranchCode claims.</summary>
public static class StaffPrincipal
{
    public static ClaimsPrincipal Create(AuthenticatedStaff staff)
    {
        ArgumentNullException.ThrowIfNull(staff);
        Claim[] claims =
        [
            new(ClaimTypes.Name, staff.Name),
            new(ApexClaimTypes.StaffId, staff.StaffId.ToString("D", CultureInfo.InvariantCulture)),
            new(ClaimTypes.Role, staff.Role.ToString()),
            new(ApexClaimTypes.BranchCode, staff.Branch.Value),
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
