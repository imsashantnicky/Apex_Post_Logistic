using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Services;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Personnel;
using ApexPost.Web.Filters;
using ApexPost.Web.Security;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

public sealed class CatalogueController(
    CatalogueService catalogue,
    StockService stock,
    PriceRevisionService prices,
    BranchService branches,
    ICurrentUser user) : ApexController
{
    private const string SkuField = nameof(StockAdjustmentCommand.Sku);

    [Authorize(Policy = ApexPolicies.SameBranchOnly)]
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        ProductCategory? category,
        string? branch,
        CancellationToken cancellationToken)
    {
        if (!TryReadBranch(branch, out var code))
        {
            return BadRequest();
        }

        var selected = code ?? user.Branch;
        var items = await catalogue.SearchAsync(new CatalogueQuery(search, category, selected), cancellationToken);
        IReadOnlyList<BranchSummary> choices = user.SeesAllBranches ? await branches.ListAsync(cancellationToken) : [];
        return View(new CataloguePage(
            items,
            search,
            category,
            new BranchFilter(choices, selected, AllowAll: false),
            user.Role == StaffRole.BranchManager,
            user.Role == StaffRole.Administrator));
    }

    [Authorize(Policy = ApexPolicies.BranchManagerOnly)]
    [HttpGet]
    public async Task<IActionResult> Adjust(string sku, CancellationToken cancellationToken)
    {
        var matches = await catalogue.SearchAsync(new CatalogueQuery(sku), cancellationToken);
        return matches.FirstOrDefault(item => item.Sku.Value == sku) is { } item ? View(item) : NotFound();
    }

    [Authorize(Policy = ApexPolicies.BranchManagerOnly)]
    [Audit(AuditEvents.StockAdjustment)]
    [HttpPost]
    public async Task<IActionResult> Adjust(StockAdjustmentCommand command, CancellationToken cancellationToken)
    {
        var result = await stock.AdjustAsync(command, cancellationToken);
        FlashSuccess($"{result.Sku} now has {result.NewQuantity} in stock.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Plain arguments, so that the audit entry keeps the category, the percent and the reason.</summary>
    [Authorize(Policy = ApexPolicies.AdministratorOnly)]
    [IpRestricted(IpAllowlist.Admin)]
    [Audit(AuditEvents.PriceRevision)]
    [HttpPost]
    public async Task<IActionResult> RevisePrices(
        ProductCategory? category,
        decimal percent,
        string? reason,
        CancellationToken cancellationToken)
    {
        var command = new PriceRevisionCommand
        {
            Category = category,
            Percent = percent,
            Reason = reason ?? string.Empty,
        };
        var changed = await prices.ReviseAsync(command, cancellationToken);
        FlashSuccess($"{changed} prices changed. Open baskets keep the prices they were built with.");
        return RedirectToAction(nameof(Index));
    }

    protected internal override IActionResult AfterFormError() =>
        Request.Form.ContainsKey(SkuField)
            ? RedirectToAction(nameof(Adjust), new { sku = Request.Form[SkuField].ToString() })
            : RedirectToAction(nameof(Index));
}
