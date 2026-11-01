using ApexPost.Application.Abstractions;
using ApexPost.Web.Middleware;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;

/// <summary>The courier partners' endpoint. Partners present an API key instead of signing in.</summary>
[AllowAnonymous]
[EnableCors(ApexCors.PartnerWidget)]
[EnableRateLimiting(ApexRateLimits.Partner)]
[IpRestricted(IpAllowlist.Integration)]
[RequireApiKey]
public sealed class IntegrationController(ICourierTrackingService courier) : Controller
{
    private const int MaxTrackingNumberLength = 32;
    private const string InvalidTrackingNumber = "The tracking number is not valid.";

    [HttpGet]
    public async Task<IActionResult> ParcelStatus(string? trackingNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber) || trackingNumber.Length > MaxTrackingNumberLength)
        {
            return ProblemResponses.ToActionResult(ProblemResponses.Create(
                HttpContext,
                StatusCodes.Status400BadRequest,
                InvalidTrackingNumber));
        }

        return Json(await courier.GetStatusAsync(trackingNumber, cancellationToken));
    }
}
