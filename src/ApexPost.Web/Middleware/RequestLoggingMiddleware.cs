using System.Diagnostics;
using System.Globalization;

namespace ApexPost.Web.Middleware;

/// <summary>Times every request, adds the time to the response and writes one structured log entry.</summary>
public sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private const string WholeMilliseconds = "0";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = Stopwatch.GetTimestamp();
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[ApexHeaders.ResponseTime] = Stopwatch.GetElapsedTime(started)
                .TotalMilliseconds.ToString(WholeMilliseconds, CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        finally
        {
            string path = context.Request.Path;
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogRequest(context.Request.Method, path, context.Response.StatusCode, elapsedMs);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Method} {Path} answered {Status} in {ElapsedMs:0} ms")]
    private partial void LogRequest(string method, string path, int status, double elapsedMs);
}
