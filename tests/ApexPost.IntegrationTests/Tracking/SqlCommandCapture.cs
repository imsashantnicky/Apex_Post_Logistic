using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApexPost.IntegrationTests.Tracking;

/// <summary>
/// Records the text of every SQL command a context sends, so a test can prove that a filter, a page or a concurrency
/// check really went to SQL Server instead of being done in memory. The commands still run unchanged.
/// </summary>
internal sealed class SqlCommandCapture : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> _commands = new();

    /// <summary>The command texts in the order they were sent.</summary>
    public IReadOnlyList<string> Commands => [.. _commands];

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>Forgets what was recorded so far, so the next assertion sees only the next commands.</summary>
    public void Clear() => _commands.Clear();

    private void Record(DbCommand command) => _commands.Enqueue(command.CommandText);
}
