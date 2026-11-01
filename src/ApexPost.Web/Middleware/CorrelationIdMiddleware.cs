using System.Globalization;
using System.Text.RegularExpressions;

namespace ApexPost.Web.Middleware;

/// <summary>
/// Gives every request one correlation ID: a safe incoming header or a new value. It becomes the request's
/// TraceIdentifier, the response header and a logging scope, so every log entry of the request carries it.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private const string PlainGuidFormat = "N";

    private static readonly Func<ILogger, string, IDisposable?> CorrelationScope =
        LoggerMessage.DefineScope<string>("CorrelationId:{CorrelationId}");

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = ReadOrCreate(context.Request);
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[ApexHeaders.CorrelationId] = correlationId;
            return Task.CompletedTask;
        });

        using (CorrelationScope(logger, correlationId))
        {
            await next(context);
        }
    }

    private static string ReadOrCreate(HttpRequest request)
    {
        var incoming = request.Headers[ApexHeaders.CorrelationId].ToString();
        return SafeId().IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString(PlainGuidFormat, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"\A[A-Za-z0-9._-]{1,64}\z")]
    private static partial Regex SafeId();
}
