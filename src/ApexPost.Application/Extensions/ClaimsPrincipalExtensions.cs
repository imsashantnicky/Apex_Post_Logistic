using System.Security.Claims;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Extensions;

/// <summary>Typed access to the StaffId, Role and BranchCode claims; null when a claim is missing or invalid.</summary>
public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal principal)
    {
        public Guid? StaffId =>
            Guid.TryParse(principal.FindFirst(ApexClaimTypes.StaffId)?.Value, out var id) && id != Guid.Empty
                ? id
                : null;

        public StaffRole? Role =>
            Enum.TryParse<StaffRole>(principal.FindFirst(ClaimTypes.Role)?.Value, out var role) && Enum.IsDefined(role)
                ? role
                : null;

        public BranchCode? BranchCode => principal.FindFirst(ApexClaimTypes.BranchCode)?.Value is { } code
            ? ParseBranch(code)
            : null;
    }

    private static BranchCode? ParseBranch(string code)
    {
        try
        {
            return new BranchCode(code);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}
