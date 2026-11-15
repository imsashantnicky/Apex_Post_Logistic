using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.ApiClient.Resilience;

/// <summary>
/// Log messages of the resilience pipeline. They hold the method, the path, the attempt, the delay and a short
/// reason (status code or exception type); never a token, a header, a query string or a body.
/// </summary>
internal static partial class ResilienceLog
{
    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "Retry {Attempt} of {MaxAttempts} for {Method} {Path} in {DelayMs} ms after {Reason}")]
    public static partial void Retrying(
        ILogger logger,
        int attempt,
        int maxAttempts,
        string method,
        string path,
        long delayMs,
        string reason);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Error,
        Message = "Circuit opened for {BreakSeconds} s after {Reason}; the app works offline")]
    public static partial void CircuitOpened(ILogger logger, double breakSeconds, string reason);

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Information,
        Message = "Circuit half-open: the next call tests the server")]
    public static partial void CircuitHalfOpened(ILogger logger);

    [LoggerMessage(
        EventId = 4104,
        Level = LogLevel.Information,
        Message = "Circuit closed: the server answers again")]
    public static partial void CircuitClosed(ILogger logger);
}
