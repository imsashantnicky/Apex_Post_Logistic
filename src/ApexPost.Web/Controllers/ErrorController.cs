using ApexPost.Web.Middleware;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>
/// The target of the re-executed error responses. It keeps the method of the failed request, so it accepts any
/// verb and skips antiforgery: a rejected POST must reach this page instead of failing again.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class ErrorController : Controller
{
    private const int MaxStatus = 599;

    public IActionResult Status(int id)
    {
        if (id is < StatusCodes.Status400BadRequest or > MaxStatus)
        {
            return NotFound();
        }

        var problem = ProblemResponses.Recall(HttpContext) ?? ProblemResponses.Create(HttpContext, id);
        if (!ProblemResponses.WantsHtml(HttpContext))
        {
            return ProblemResponses.ToActionResult(problem);
        }

        Response.StatusCode = problem.Status ?? id;
        return View(StatusPage.From(problem));
    }
}
