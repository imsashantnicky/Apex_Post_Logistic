using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Services;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Personnel;
using ApexPost.Web.Security;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

public sealed class CatalogueController(
    CatalogueService catalogue,
    StockService stock,
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
            user.Role == StaffRole.BranchManager));
    }

    [Authorize(Policy = ApexPolicies.BranchManagerOnly)]
    [HttpGet]
    public async Task<IActionResult> Adjust(string sku, CancellationToken cancellationToken)
    {
        var matches = await catalogue.SearchAsync(new CatalogueQuery(sku), cancellationToken);
        return matches.FirstOrDefault(item => item.Sku.Value == sku) is { } item ? View(item) : NotFound();
    }

    [Authorize(Policy = ApexPolicies.BranchManagerOnly)]
    [HttpPost]
    public async Task<IActionResult> Adjust(StockAdjustmentCommand command, CancellationToken cancellationToken)
    {
        var result = await stock.AdjustAsync(command, cancellationToken);
        FlashSuccess($"{result.Sku} now has {result.NewQuantity} in stock.");
        return RedirectToAction(nameof(Index));
    }

    protected override IActionResult AfterFormError() =>
        RedirectToAction(nameof(Adjust), new { sku = Request.Form[SkuField].ToString() });
}
