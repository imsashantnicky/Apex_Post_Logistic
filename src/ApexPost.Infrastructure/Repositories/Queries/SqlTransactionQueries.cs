using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Services;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories.Queries;

/// <summary>
/// The list costs three queries whatever the page size: the total, the page and all lines of that page. Open baskets
/// need their lines for the tax preview, which is a domain rule and so runs here, not in SQL.
/// </summary>
public sealed class SqlTransactionQueries(IDbContextFactory<ApexPostDbContext> contexts, TaxCalculator tax)
    : ITransactionQueries
{
    private const string TransactionKey = "TransactionId";

    public async Task<PagedResult<TransactionSummary>> ListAsync(
        TransactionFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(page);
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            var matching = Matching(database, filter);
            var total = await matching.CountAsync(cancellationToken).ConfigureAwait(false);
            var rows = await PageOf(matching, page).ToListAsync(cancellationToken).ConfigureAwait(false);

            Guid[] ids = [.. rows.Select(row => row.Id)];
            var lines = (await database.Set<TransactionLine>()
                    .AsNoTracking()
                    .Where(line => ids.Contains(EF.Property<Guid>(line, TransactionKey)))
                    .Select(line => new { TransactionId = EF.Property<Guid>(line, TransactionKey), Line = line })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .ToLookup(row => row.TransactionId, row => (ILineItem)row.Line);

            return new PagedResult<TransactionSummary>(
                [
                    .. rows.Select(row => new TransactionSummary(
                        row.Id,
                        row.Branch,
                        row.OpenedAt,
                        row.Status,
                        lines[row.Id].Count(),
                        TotalOf(lines[row.Id], row.Payment))),
                ],
                page.Number,
                page.Size,
                total);
        }
    }

    public async Task<CounterTransaction?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            return await TransactionLoader
                .LoadAsync(database, database.Set<CounterTransaction>().AsNoTracking(), id, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal static IQueryable<TransactionPageRow> PageOf(IQueryable<CounterTransaction> matching, PageRequest page) =>
        matching
            .OrderByDescending(transaction => transaction.OpenedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip(page.Skip)
            .Take(page.Size)
            .Select(transaction => new TransactionPageRow
            {
                Id = transaction.Id,
                Branch = transaction.Branch,
                OpenedAt = transaction.OpenedAt,
                Status = transaction.Status,
                Payment = transaction.Payment,
            });

    internal static IQueryable<CounterTransaction> Matching(ApexPostDbContext database, TransactionFilter filter)
    {
        var transactions = database.Set<CounterTransaction>().AsNoTracking();
        if (filter.Branch is { } branch)
        {
            transactions = transactions.Where(transaction => transaction.Branch == branch);
        }

        if (filter.From is { } from)
        {
            var start = ReportDays.Start(from);
            transactions = transactions.Where(transaction => transaction.OpenedAt >= start);
        }

        if (filter.To is { } to)
        {
            var end = ReportDays.EndOf(to);
            transactions = transactions.Where(transaction => transaction.OpenedAt < end);
        }

        return filter.Status is { } status
            ? transactions.Where(transaction => transaction.Status == status)
            : transactions;
    }

    private Money TotalOf(IEnumerable<ILineItem> lines, Payment? payment)
    {
        ILineItem[] basket = [.. lines];
        var taxAmount = payment?.Tax ?? basket.Select(tax.Rule).Total();
        return basket.Select(line => line.Total).Total() + taxAmount;
    }

    /// <summary>The header of one transaction on a page of the list.</summary>
    internal sealed class TransactionPageRow
    {
        public Guid Id { get; init; }

        public BranchCode Branch { get; init; } = null!;

        public DateTimeOffset OpenedAt { get; init; }

        public TransactionStatus Status { get; init; }

        public Payment? Payment { get; init; }
    }
}
