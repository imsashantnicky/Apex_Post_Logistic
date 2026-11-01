using ApexPost.Application.DTOs;
using ApexPost.Application.Services;
using ApexPost.Web.Filters;
using ApexPost.Web.Security;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>The audit log for auditors and administrators: a paged list and a JSON export.</summary>
[Authorize(Policy = ApexPolicies.AuditViewers)]
[IpRestricted(IpAllowlist.Admin)]
public sealed class AuditController(AuditService audit) : ApexController
{
    private const string ExportDisposition = "attachment; filename=audit.json";

    [Audit(AuditEvents.AuditViewed)]
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default) =>
        View(await audit.ReadAsync(new PageRequest(page), cancellationToken));

    [Audit(AuditEvents.AuditExported)]
    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var entries = await audit.ExportAsync(cancellationToken);
        Response.Headers.ContentDisposition = ExportDisposition;
        return Json(entries.Select(AuditRow.From));
    }
}
