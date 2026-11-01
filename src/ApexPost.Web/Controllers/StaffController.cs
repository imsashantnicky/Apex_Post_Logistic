using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Services;
using ApexPost.Web.Filters;
using ApexPost.Web.Security;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>
/// Staff administration for administrators on an allowed address. The form models are never audited; the
/// deactivation reason is a plain argument so that the audit entry keeps it.
/// </summary>
[Authorize(Policy = ApexPolicies.AdministratorOnly)]
[IpRestricted(IpAllowlist.Admin)]
public sealed class StaffController(StaffService staff, BranchService branches, ICurrentUser user) : ApexController
{
    private const string IdRouteValue = "id";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await staff.ListAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View(new StaffFormPage(null, await branches.ListAsync(cancellationToken), staff.MinimumPasswordLength));

    [Audit(AuditEvents.StaffCreated)]
    [HttpPost]
    public async Task<IActionResult> Create(
        StaffCommand details,
        PasswordCommand secret,
        CancellationToken cancellationToken)
    {
        var id = await staff.CreateAsync(details, secret, cancellationToken);
        FlashSuccess("Staff member added.");
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var member = await staff.GetAsync(id, cancellationToken);
        return View(new StaffFormPage(
            member,
            await branches.ListAsync(cancellationToken),
            staff.MinimumPasswordLength,
            id == user.StaffId));
    }

    [Audit(AuditEvents.StaffUpdated)]
    [HttpPost]
    public async Task<IActionResult> Edit(
        Guid id,
        int version,
        StaffCommand details,
        CancellationToken cancellationToken)
    {
        await staff.UpdateAsync(id, version, details, cancellationToken);
        FlashSuccess("Staff member saved.");
        return RedirectToAction(nameof(Index));
    }

    [Audit(AuditEvents.PasswordChanged)]
    [HttpPost]
    public async Task<IActionResult> SetPassword(Guid id, PasswordCommand secret, CancellationToken cancellationToken)
    {
        await staff.SetPasswordAsync(id, secret, cancellationToken);
        return BackToMember(id, "Password changed.");
    }

    [Audit(AuditEvents.StaffDeactivated)]
    [HttpPost]
    public async Task<IActionResult> Deactivate(
        Guid id,
        int version,
        string? reason,
        CancellationToken cancellationToken)
    {
        var command = new DeactivationCommand { Reason = reason ?? string.Empty };
        await staff.DeactivateAsync(id, version, command, cancellationToken);
        return BackToMember(id, "Staff member deactivated. Their session has ended.");
    }

    [Audit(AuditEvents.StaffActivated)]
    [HttpPost]
    public async Task<IActionResult> Activate(Guid id, int version, CancellationToken cancellationToken)
    {
        await staff.ActivateAsync(id, version, cancellationToken);
        return BackToMember(id, "Staff member activated.");
    }

    protected internal override IActionResult AfterFormError() =>
        Guid.TryParse(RouteData.Values[IdRouteValue]?.ToString(), out var id)
            ? RedirectToAction(nameof(Edit), new { id })
            : RedirectToAction(nameof(Create));

    private RedirectToActionResult BackToMember(Guid id, string message)
    {
        FlashSuccess(message);
        return RedirectToAction(nameof(Edit), new { id });
    }
}
