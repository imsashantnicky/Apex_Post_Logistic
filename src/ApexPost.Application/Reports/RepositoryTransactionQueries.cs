using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Services;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Reports;

/// <summary>
/// The transaction list built in memory from the repository, newest first. Open baskets show the tax preview,
/// so a list never shows a total without tax. A database version replaces this class with a SQL query.
/// </summary>
public sealed class RepositoryTransactionQueries(
    IRepository<CounterTransaction, Guid> transactions,
    TaxCalculator tax) : ITransactionQueries
{
    public async Task<PagedResult<TransactionSummary>> ListAsync(
        TransactionFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var matching = await transactions.ListAsync(ReportSpecifications.Matching(filter), cancellationToken)
            .ConfigureAwait(false);
        TransactionSummary[] rows =
        [
            .. matching
                .OrderByDescending(transaction => transaction.OpenedAt)
                .Select(transaction => new TransactionSummary(
                    transaction.Id,
                    transaction.Branch,
                    transaction.OpenedAt,
                    transaction.Status,
                    transaction.Lines.Count,
                    transaction.Subtotal + tax.Preview(transaction))),
        ];
        return rows.ToPage(page);
    }

    public Task<CounterTransaction?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        transactions.GetAsync(id, cancellationToken);
}
