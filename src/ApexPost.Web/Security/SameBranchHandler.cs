using ApexPost.Application.Abstractions;
using ApexPost.Application.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Security;

/// <summary>Compares the branch named in the route or query with the BranchCode claim.</summary>
public sealed class SameBranchHandler(ICurrentUser user) : AuthorizationHandler<SameBranchRequirement>
{
    public const string BranchParameter = "branch";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameBranchRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.User.BranchCode is { } own && context.Resource is HttpContext http)
        {
            var requested = http.GetRouteValue(BranchParameter)?.ToString() ?? http.Request.Query[BranchParameter];
            if (string.IsNullOrEmpty(requested)
                || user.SeesAllBranches
                || string.Equals(requested, own.Value, StringComparison.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
