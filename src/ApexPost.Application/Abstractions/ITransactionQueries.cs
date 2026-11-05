using ApexPost.Application.DTOs;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Abstractions;

/// <summary>The paged transaction list and a read-only copy of one transaction.</summary>
public interface ITransactionQueries
{
    Task<PagedResult<TransactionSummary>> ListAsync(
        TransactionFilter filter,
        PageRequest page,
        CancellationToken cancellationToken);

    /// <summary>A copy for reading: tax preview and cash rounding are domain rules, so it is the aggregate.</summary>
    Task<CounterTransaction?> FindAsync(Guid id, CancellationToken cancellationToken);
}
