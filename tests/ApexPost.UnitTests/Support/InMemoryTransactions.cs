using ApexPost.Application.Abstractions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;

namespace ApexPost.UnitTests.Support;

/// <summary>The transaction ports on top of the in-memory store, which keeps checks inside the aggregate.</summary>
internal sealed class InMemoryTransactions(
    InMemoryRepository<CounterTransaction, Guid> store,
    FakeUnitOfWork unitOfWork) : ICounterTransactionRepository, IIdentityVerificationRepository
{
    public Task<CounterTransaction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        store.GetAsync(id, cancellationToken);

    public void Add(CounterTransaction transaction) =>
        unitOfWork.Stage(token => store.AddAsync(transaction, token));

    public void Update(CounterTransaction transaction) =>
        unitOfWork.Stage(token => store.UpdateAsync(transaction, transaction.Version, token));

    public async Task<bool> HasOpenBasketAsync(Guid counterId, CancellationToken cancellationToken) =>
        (await store.ListAsync(ReportSpecifications.OpenAtCounter(counterId), cancellationToken)).Count > 0;

    public void Add(Guid transactionId, IdentityVerification verification)
    {
    }
}
