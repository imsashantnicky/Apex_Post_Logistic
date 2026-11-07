using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.ApiClient.Tests.Support;

/// <summary>Keeps every formatted log line (all categories) so tests can check what was and was not logged.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<CapturedLog> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new CapturedLog(category, logLevel, eventId.Id, formatter(state, exception)));
    }
}

public sealed record CapturedLog(string Category, LogLevel Level, int EventId, string Message);
