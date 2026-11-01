using System.Globalization;
using ApexPost.Web.Composition;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Middleware;

/// <summary>
/// While maintenance is on, answers 503 with Retry-After. /health keeps working for the load balancer, and the
/// re-executed status page passes so that browsers see the error page.
/// </summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next, IOptionsMonitor<MaintenanceSettings> settings)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = settings.CurrentValue;
        if (!current.Enabled || IsExempt(context))
        {
            await next(context);
            return;
        }

        context.Response.Headers.RetryAfter = current.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        await ProblemResponses.WriteAsync(context, StatusCodes.Status503ServiceUnavailable);
    }

    private static bool IsExempt(HttpContext context) =>
        context.Request.Path.StartsWithSegments(ApexRoutes.HealthPath, StringComparison.OrdinalIgnoreCase)
        || context.Features.Get<IStatusCodeReExecuteFeature>() is not null;
}
