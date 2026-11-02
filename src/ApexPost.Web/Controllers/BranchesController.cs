using ApexPost.Application.Services;
using ApexPost.Domain.Branches;
using ApexPost.Web.Filters;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>Branch administration for administrators on an allowed address: open, close and counters.</summary>
[Authorize(Policy = ApexPolicies.AdministratorOnly)]
[IpRestricted(IpAllowlist.Admin)]
public sealed class BranchesController(BranchService branches) : ApexController
{
    private const string IdRouteValue = "id";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await branches.ListAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken) =>
        TryReadBranch(id, out var code) && code is not null
            ? View(await branches.GetAsync(code, cancellationToken))
            : NotFound();

    [Audit(AuditEvents.BranchClosed)]
    [HttpPost]
    public Task<IActionResult> Close([FromRoute] string id, int version, CancellationToken cancellationToken) =>
        ChangeAsync(id, "Branch closed.", code => branches.CloseAsync(code, version, cancellationToken));

    [Audit(AuditEvents.BranchReopened)]
    [HttpPost]
    public Task<IActionResult> Reopen([FromRoute] string id, int version, CancellationToken cancellationToken) =>
        ChangeAsync(id, "Branch reopened.", code => branches.ReopenAsync(code, version, cancellationToken));

    [Audit(AuditEvents.CounterAdded)]
    [HttpPost]
    public Task<IActionResult> AddCounter([FromRoute] string id, int version, CancellationToken cancellationToken) =>
        ChangeAsync(id, "Counter added.", code => branches.AddCounterAsync(code, version, cancellationToken));

    [Audit(AuditEvents.CounterRemoved)]
    [HttpPost]
    public Task<IActionResult> RemoveCounter(
        [FromRoute] string id,
        Guid counterId,
        int version,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            id,
            "Counter removed.",
            code => branches.RemoveCounterAsync(code, counterId, version, cancellationToken));

    protected internal override IActionResult AfterFormError() =>
        RouteData.Values[IdRouteValue] is string id
            ? RedirectToAction(nameof(Details), new { id })
            : RedirectToAction(nameof(Index));

    private async Task<IActionResult> ChangeAsync(string id, string message, Func<BranchCode, Task> change)
    {
        if (!TryReadBranch(id, out var code) || code is null)
        {
            return NotFound();
        }

        await change(code);
        FlashSuccess(message);
        return RedirectToAction(nameof(Details), new { id });
    }
}
