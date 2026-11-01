using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ApexPost.IntegrationTests.Support;

/// <summary>Keeps every log entry together with the scopes that were active when it was written.</summary>
public sealed class CapturedLogs : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyCollection<CapturedLog> Entries => _entries;

    /// <summary>The pipeline logs after the client has its response, so an entry may still be on its way.</summary>
    public async Task<CapturedLog> WaitForAsync(Func<CapturedLog, bool> match)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_entries.FirstOrDefault(match) is { } entry)
            {
                return entry;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("The expected log entry was not written.");
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private void Add(string category, LogLevel level, string message)
    {
        var scopes = new List<string>();
        _scopes.ForEachScope(
            static (scope, list) => list.AddRange(ScopeValues(scope)),
            scopes);
        _entries.Enqueue(new CapturedLog(category, level, message, scopes));
    }

    private static IEnumerable<string> ScopeValues(object? scope) =>
        scope is IEnumerable<KeyValuePair<string, object>> values
            ? values.Select(pair => $"{pair.Key}={pair.Value}")
            : [];

    private sealed class Logger(CapturedLogs logs, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            logs._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            logs.Add(category, logLevel, formatter(state, exception));
    }
}
