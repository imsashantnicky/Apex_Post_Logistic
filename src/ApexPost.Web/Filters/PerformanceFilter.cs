using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Filters;

/// <summary>
/// Warns about slow actions. It is a filter because it measures the action alone and can name it; the request
/// timing middleware covers the whole pipeline but only knows the URL.
/// </summary>
public sealed partial class PerformanceFilter(
    IOptionsMonitor<PerformanceSettings> settings,
    ILogger<PerformanceFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var started = Stopwatch.GetTimestamp();
        await next();

        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (elapsedMs > settings.CurrentValue.SlowActionMs)
        {
            var action = context.ActionDescriptor.DisplayName;
            LogSlowAction(action, elapsedMs);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Action} took {ElapsedMs:0} ms")]
    private partial void LogSlowAction(string? action, double elapsedMs);
}
