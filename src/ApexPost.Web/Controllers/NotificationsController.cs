using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Reports;
using ApexPost.Application.Services;
using ApexPost.Web.Security;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>The simulated notifications: low stock, high-value money orders, voids and refunds.</summary>
public sealed class NotificationsController(ReportService reports, BranchService branches, ICurrentUser user)
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

        var items = await reports.NotificationsAsync(code, cancellationToken);
        return View(new ReportPage<IReadOnlyList<Notification>>(items, await ToolbarAsync(code, cancellationToken)));
    }
}
