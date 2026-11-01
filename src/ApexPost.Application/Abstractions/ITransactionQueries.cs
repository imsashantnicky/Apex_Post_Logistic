using ApexPost.Application.DTOs;

namespace ApexPost.Application.Abstractions;

/// <summary>The paged transaction list; a database can answer it with one SQL query.</summary>
public interface ITransactionQueries
{
    Task<PagedResult<TransactionSummary>> ListAsync(
        TransactionFilter filter,
        PageRequest page,
        CancellationToken cancellationToken);
}
