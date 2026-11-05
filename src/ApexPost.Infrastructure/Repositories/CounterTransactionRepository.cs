using ApexPost.Application.Abstractions;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories;

public sealed class CounterTransactionRepository(ApexPostDbContext database) : ICounterTransactionRepository
{
    public Task<CounterTransaction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        TransactionLoader.LoadAsync(database, database.Set<CounterTransaction>(), id, cancellationToken);

    public void Add(CounterTransaction transaction) => database.Add(transaction);

    /// <summary>
    /// Adding a line or a check inserts another table's row and leaves the header alone. Touching the header anyway
    /// puts every change through its RowVersion, so a line cannot slip into a basket that was paid meanwhile.
    /// </summary>
    public void Update(CounterTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        database.Entry(transaction).Property(ColumnRules.ModifiedAt).IsModified = true;
    }

    public Task<bool> HasOpenBasketAsync(Guid counterId, CancellationToken cancellationToken) =>
        database.Set<CounterTransaction>()
            .AsNoTracking()
            .AnyAsync(
                transaction => transaction.CounterId == counterId && transaction.Status == TransactionStatus.Open,
                cancellationToken);
}
