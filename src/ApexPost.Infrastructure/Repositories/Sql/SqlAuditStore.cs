using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Mappers;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Repositories.Sql;

/// <summary>
/// The audit log in the AuditEntries table. An entry is written with its own short-lived context, after the business
/// commit: it can neither roll a sale back nor be lost when a rejected sale rolls back. A failed write is logged with
/// the correlation ID and never reaches the customer at the counter.
/// </summary>
public sealed partial class SqlAuditStore(
    IDbContextFactory<ApexPostDbContext> contexts,
    ILogger<SqlAuditStore> logger) : IAuditStore
{
    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (database.ConfigureAwait(false))
            {
                database.Add(AuditEntryMapper.ToRecord(entry));
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogAppendFailed(logger, exception, entry.Event, entry.CorrelationId);
        }
    }

    public async Task<PagedResult<AuditEntry>> ReadAsync(PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            var entries = database.Set<AuditEntryRecord>().AsNoTracking();
            var total = await entries.CountAsync(cancellationToken).ConfigureAwait(false);
            var records = await entries
                .OrderByDescending(record => record.At)
                .ThenByDescending(record => record.Id)
                .Skip(page.Skip)
                .Take(page.Size)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return new PagedResult<AuditEntry>(
                [.. records.Select(AuditEntryMapper.ToEntry)],
                page.Number,
                page.Size,
                total);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Audit entry {Event} was not stored (correlation ID {CorrelationId})")]
    private static partial void LogAppendFailed(
        ILogger logger,
        Exception exception,
        string @event,
        string correlationId);
}
