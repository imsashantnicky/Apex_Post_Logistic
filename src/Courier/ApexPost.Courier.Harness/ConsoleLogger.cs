using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Harness;

/// <summary>
/// A minimal synchronous logger, so lifecycle messages such as "Listener token removed" and "Database closed"
/// appear on the console in order, before the process ends.
/// </summary>
internal sealed class ConsoleLogger<T>(TextWriter output, LogLevel minimumLevel = LogLevel.Information) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        // Only the exception type and message are shown; the harness never prints stack traces.
        var reason = exception is null ? string.Empty : $" ({exception.GetType().Name}: {exception.Message})";
        output.WriteLine($"[{Label(logLevel)}] {formatter(state, exception)}{reason}");
    }

    private static string Label(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Warning => "warn",
        LogLevel.Error or LogLevel.Critical => "error",
        LogLevel.Information => "info",
        _ => "debug",
    };
}
