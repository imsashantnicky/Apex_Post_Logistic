using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Services;
using ApexPost.Domain.Branches;
using ApexPost.Web.ViewModels;

namespace ApexPost.Web.Controllers;

/// <summary>Pages limited to one branch or, for staff who work across branches, to any branch or all of them.</summary>
public abstract class BranchScopedController(BranchService branches, ICurrentUser user) : ApexController
{
    private const string AllBranches = "All branches";

    protected ICurrentUser CurrentUser => user;

    /// <summary>The filter bar and the name of what is shown; an unknown branch ends the request with 404.</summary>
    protected async Task<ReportToolbar> ToolbarAsync(
        BranchCode? requested,
        CancellationToken cancellationToken,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        var scope = user.ScopeTo(requested);
        var label = scope is null ? AllBranches : BranchLabel(await branches.GetAsync(scope, cancellationToken));
        IReadOnlyList<BranchSummary> choices = user.SeesAllBranches ? await branches.ListAsync(cancellationToken) : [];
        return new ReportToolbar(new BranchFilter(choices, scope, AllowAll: true), label, from, to);
    }

    private static string BranchLabel(BranchSummary branch) => $"{branch.Name} ({branch.Code})";
}
