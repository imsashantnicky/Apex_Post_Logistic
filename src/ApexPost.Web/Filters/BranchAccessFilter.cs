using ApexPost.Application.Abstractions;
using ApexPost.Application.Extensions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

/// <summary>
/// Blocks any action whose bound "branch" argument names another branch, wherever the value came from, so that a
/// new action cannot forget the check. It is a filter because the SameBranchOnly policy runs before model binding
/// and sees only the route and query. A text that is not a branch code is left to the action, which rejects it.
/// </summary>
public sealed class BranchAccessFilter(ICurrentUser user) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (context.ActionArguments.TryGetValue(SameBranchHandler.BranchParameter, out var value)
            && IsForeign(value?.ToString()))
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }

    private bool IsForeign(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch) || user.SeesAllBranches)
        {
            return false;
        }

        try
        {
            return new BranchCode(branch) != user.Branch;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
