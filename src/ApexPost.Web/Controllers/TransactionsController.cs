using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Services;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Web.Filters;
using ApexPost.Web.Security;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>The counter: the transaction list, a new basket, the counter screen and the shipping label.</summary>
public sealed class TransactionsController(
    CheckoutService checkout,
    ReversalService reversals,
    CatalogueService catalogue,
    BranchService branches,
    IZoneCatalog zones,
    CheckoutRules rules,
    ICurrentUser user) : ApexController
{
    private const string IdRouteValue = "id";

    [Authorize(Policy = ApexPolicies.SameBranchOnly)]
    [HttpGet]
    public async Task<IActionResult> Index(
        TransactionStatus? status,
        string? branch,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadBranch(branch, out var code))
        {
            return BadRequest();
        }

        var result = await checkout.ListAsync(
            new TransactionFilter(code, Status: status),
            new PageRequest(page),
            cancellationToken);
        IReadOnlyList<BranchSummary> choices = user.SeesAllBranches ? await branches.ListAsync(cancellationToken) : [];
        return View(new TransactionListPage(
            result,
            status,
            new BranchFilter(choices, code, AllowAll: true),
            user.CanTransact));
    }

    [Authorize(Policy = ApexPolicies.CanTransact)]
    [HttpGet]
    public async Task<IActionResult> New(CancellationToken cancellationToken) =>
        View(await branches.GetAsync(user.Branch, cancellationToken));

    [Authorize(Policy = ApexPolicies.CanTransact)]
    [Audit(AuditEvents.TransactionOpened)]
    [HttpPost]
    public async Task<IActionResult> Create(Guid counterId, CancellationToken cancellationToken)
    {
        var id = await checkout.OpenAsync(counterId, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await checkout.GetAsync(id, cancellationToken);
        var isOpen = transaction.Status == TransactionStatus.Open;
        var branchClosed = isOpen && !(await branches.GetAsync(transaction.Branch, cancellationToken)).IsActive;
        var canWork = user.CanTransact && isOpen && !branchClosed;
        IReadOnlyList<CatalogueItem> products = canWork
            ? await catalogue.SearchAsync(new CatalogueQuery(), cancellationToken)
            : [];
        IReadOnlyList<ShippingZone> shippingZones = canWork ? await zones.ListAsync(cancellationToken) : [];
        return View(new CounterPage(
            transaction,
            products,
            shippingZones,
            rules.VerificationThreshold,
            canWork,
            user.Role == StaffRole.BranchManager,
            branchClosed));
    }

    [HttpGet]
    public async Task<IActionResult> Label(Guid id, Guid lineId, CancellationToken cancellationToken)
    {
        var label = await checkout.GetLabelAsync(id, lineId, cancellationToken);
        ViewData[IdRouteValue] = id;
        return View(label);
    }

    [Authorize(Policy = ApexPolicies.CanTransact)]
    [Audit(AuditEvents.RetailItemAdded)]
    [HttpPost]
    public async Task<IActionResult> AddRetail(
        Guid id,
        AddRetailItemCommand command,
        CancellationToken cancellationToken)
    {
        await checkout.AddRetailItemAsync(id, command, cancellationToken);
        return BackToCounter(id, "Item added to the basket.");
    }

    [Authorize(Policy = ApexPolicies.CanTransact)]
    [Audit(AuditEvents.ShipmentAdded)]
    [HttpPost]
    public async Task<IActionResult> AddShipping(
        Guid id,
        AddShippingCommand command,
        CancellationToken cancellationToken)
    {
        await checkout.AddShippingAsync(id, command, cancellationToken);
        return BackToCounter(id, "Shipment added to the basket.");
    }

    [Authorize(Policy = ApexPolicies.CanTransact)]
    [Audit(AuditEvents.MoneyOrderAdded)]
    [HttpPost]
    public async Task<IActionResult> AddMoneyOrder(
        Guid id,
        AddMoneyOrderCommand command,
        CancellationToken cancellationToken)
    {
        await checkout.AddMoneyOrderAsync(id, command, cancellationToken);
        return BackToCounter(id, "Money order added to the basket.");
    }

    [Authorize(Policy = ApexPolicies.CanTransact)]
    [Audit(AuditEvents.IdentityVerification)]
    [HttpPost]
    public async Task<IActionResult> VerifyIdentity(
        Guid id,
        VerifyIdentityCommand command,
        CancellationToken cancellationToken)
    {
        var result = await checkout.VerifyIdentityAsync(id, command, cancellationToken);
        return BackToCounter(id, $"Identity verification recorded: {result}.");
    }

    [Authorize(Policy = ApexPolicies.CanTransact)]
    [Audit(AuditEvents.Payment)]
    [HttpPost]
    public async Task<IActionResult> Pay(Guid id, PaymentMethod method, CancellationToken cancellationToken)
    {
        await checkout.PayAsync(id, method, cancellationToken);
        return BackToCounter(id, "Payment taken.");
    }

    [Authorize(Policy = ApexPolicies.BranchManagerOnly)]
    [Audit(AuditEvents.Void)]
    [HttpPost]
    public async Task<IActionResult> Void(Guid id, ReversalCommand command, CancellationToken cancellationToken)
    {
        await reversals.VoidAsync(id, command, cancellationToken);
        return BackToCounter(id, "Transaction voided.");
    }

    [Authorize(Policy = ApexPolicies.BranchManagerOnly)]
    [Audit(AuditEvents.Refund)]
    [HttpPost]
    public async Task<IActionResult> Refund(Guid id, ReversalCommand command, CancellationToken cancellationToken)
    {
        await reversals.RefundAsync(id, command, cancellationToken);
        return BackToCounter(id, "Transaction refunded.");
    }

    protected internal override IActionResult AfterFormError() =>
        Guid.TryParse(RouteData.Values[IdRouteValue]?.ToString(), out var id)
            ? RedirectToAction(nameof(Details), new { id })
            : RedirectToAction(nameof(New));

    private RedirectToActionResult BackToCounter(Guid id, string message)
    {
        FlashSuccess(message);
        return RedirectToAction(nameof(Details), new { id });
    }
}
