using ApexPost.Application.Abstractions;
using ApexPost.Application.Extensions;
using ApexPost.Application.Reports;
using ApexPost.Application.Services;
using ApexPost.Web.Security;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

public sealed class DashboardController(ReportService reports, BranchService branches, ICurrentUser user)
    : BranchScopedController(branches, user)
{
    [Authorize(Policy = ApexPolicies.SameBranchOnly)]
    [HttpGet]
    public async Task<IActionResult> Index(string? branch, CancellationToken cancellationToken)
    {
        if (!TryReadBranch(branch, out var code))
        {
            return BadRequest();
        }

        var dashboard = await reports.DashboardAsync(code, cancellationToken);
        var toolbar = await ToolbarAsync(dashboard.Branch, cancellationToken);
        return View(new DashboardPage(dashboard, toolbar, CurrentUser.CanTransact, CurrentUser.CanViewReports));
    }
}
