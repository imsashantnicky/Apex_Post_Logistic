using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Abstractions;

/// <summary>Transactions for writing; nothing is stored until the unit of work commits.</summary>
public interface ICounterTransactionRepository
{
    /// <summary>The whole aggregate, ready to be changed.</summary>
    Task<CounterTransaction?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(CounterTransaction transaction);

    /// <summary>Stages a change that fails with a conflict if someone else changed the transaction meanwhile.</summary>
    void Update(CounterTransaction transaction);

    Task<bool> HasOpenBasketAsync(Guid counterId, CancellationToken cancellationToken);
}
