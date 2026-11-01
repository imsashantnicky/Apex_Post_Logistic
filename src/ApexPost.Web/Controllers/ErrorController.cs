using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>
/// The target of the re-executed error responses. It keeps the method of the failed request, so it accepts any
/// verb and skips antiforgery: a rejected POST must reach this page instead of failing again. A direct visit
/// finds nothing, so nobody can write access-denied entries into the audit log by opening a URL. The denied
/// request is audited here because authorization rejects it before any action of its own runs.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class ErrorController : Controller
{
    private const int MaxStatus = 599;

    [Audit(AuditEvents.AccessDenied, OnlyForStatus = StatusCodes.Status403Forbidden)]
    public IActionResult Status(int id)
    {
        if (!IsReExecuted || id is < StatusCodes.Status400BadRequest or > MaxStatus)
        {
            return NotFound();
        }

        var problem = ProblemResponses.Recall(HttpContext) ?? ProblemResponses.Create(HttpContext, id);
        Response.StatusCode = problem.Status ?? id;
        return ProblemResponses.WantsHtml(HttpContext)
            ? View(StatusPage.From(problem))
            : ProblemResponses.ToActionResult(problem);
    }

    private bool IsReExecuted =>
        HttpContext.Features.Get<IStatusCodeReExecuteFeature>() is not null
        || HttpContext.Features.Get<IExceptionHandlerFeature>() is not null;
}
