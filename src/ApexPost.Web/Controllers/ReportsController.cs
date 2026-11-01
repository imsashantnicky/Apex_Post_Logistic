using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Reports;
using ApexPost.Application.Services;
using ApexPost.Web.Security;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>
/// The four reports. Managers see their own branch, auditors and administrators any branch or all of them. Low
/// stock describes the present, so it has no dates.
/// </summary>
[Authorize(Policy = ApexPolicies.CanViewReports)]
[Authorize(Policy = ApexPolicies.SameBranchOnly)]
public sealed class ReportsController(ReportService reports, BranchService branches, ICurrentUser user)
    : BranchScopedController(branches, user)
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> DailySales(
        string? branch,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!TryReadBranch(branch, out var code))
        {
            return BadRequest();
        }

        var filter = reports.FilterFor(code, from, to);
        var rows = await reports.DailySalesAsync(filter, cancellationToken);
        return View(new ReportPage<IReadOnlyList<DailySalesRow>>(rows, await ToolbarAsync(filter, cancellationToken)));
    }

    [HttpGet]
    public async Task<IActionResult> LowStock(string? branch, CancellationToken cancellationToken)
    {
        if (!TryReadBranch(branch, out var code))
        {
            return BadRequest();
        }

        var report = await reports.LowStockAsync(code, cancellationToken);
        return View(new ReportPage<LowStockReport>(report, await ToolbarAsync(code, cancellationToken)));
    }

    [HttpGet]
    public async Task<IActionResult> ShippingVolume(
        string? branch,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!TryReadBranch(branch, out var code))
        {
            return BadRequest();
        }

        var filter = reports.FilterFor(code, from, to);
        var rows = await reports.ShippingVolumeAsync(filter, cancellationToken);
        return View(new ReportPage<IReadOnlyList<ShippingVolumeRow>>(
            rows,
            await ToolbarAsync(filter, cancellationToken)));
    }

    [HttpGet]
    public async Task<IActionResult> MoneyOrders(
        string? branch,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadBranch(branch, out var code))
        {
            return BadRequest();
        }

        var filter = reports.FilterFor(code, from, to);
        var report = await reports.MoneyOrdersAsync(filter, new PageRequest(page), cancellationToken);
        return View(new ReportPage<MoneyOrderReport>(report, await ToolbarAsync(filter, cancellationToken)));
    }

    private Task<ReportToolbar> ToolbarAsync(ReportFilter filter, CancellationToken cancellationToken) =>
        ToolbarAsync(filter.Branch, cancellationToken, filter.From, filter.To);
}
