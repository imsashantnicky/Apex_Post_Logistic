using ApexPost.Application.Abstractions;

namespace ApexPost.UnitTests.Support;

/// <summary>Holds what the fake repositories staged until a commit, as the real unit of work does.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly List<Func<CancellationToken, Task>> _staged = [];

    public int TransactionCount { get; private set; }

    public void Stage(Func<CancellationToken, Task> change) => _staged.Add(change);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var change in _staged)
        {
            await change(cancellationToken);
        }

        _staged.Clear();
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        TransactionCount++;
        await work(cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }
}
