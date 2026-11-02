using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApexPost.IntegrationTests.Support;

/// <summary>
/// Records every row write EF Core sends, in order, as "UPDATE BranchStock PKG-0001" or "INSERT Payments". The SKU of
/// a stock row is read from the command's parameters, because SQL text logging hides the values.
/// </summary>
internal sealed partial class WriteRecorder : DbCommandInterceptor
{
    private const string StockTable = "BranchStock";

    private readonly ConcurrentQueue<string> _writes = new();

    /// <summary>The writes recorded since the last call, oldest first; audit rows are left out (ADR-016).</summary>
    public IReadOnlyList<string> TakeWrites()
    {
        var writes = new List<string>();
        while (_writes.TryDequeue(out var write))
        {
            writes.Add(write);
        }

        return writes;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
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

    private void Record(DbCommand command)
    {
        foreach (var statement in command.CommandText.Split(';'))
        {
            if (Write().Match(statement) is not { Success: true } write || write.Groups["table"].Value == "AuditEntries")
            {
                continue;
            }

            var operation = write.Groups["operation"].Value == "UPDATE" ? "UPDATE" : "INSERT";
            var table = write.Groups["table"].Value;
            _writes.Enqueue(table == StockTable && StockKey().Match(statement) is { Success: true } key
                ? $"{operation} {table} {command.Parameters[key.Groups["sku"].Value].Value}"
                : $"{operation} {table}");
        }
    }

    [GeneratedRegex(@"(?<operation>INSERT INTO|UPDATE|MERGE) \[(?<table>\w+)\]")]
    private static partial Regex Write();

    [GeneratedRegex(@"\[Sku\] = (?<sku>@p\d+)")]
    private static partial Regex StockKey();
}
