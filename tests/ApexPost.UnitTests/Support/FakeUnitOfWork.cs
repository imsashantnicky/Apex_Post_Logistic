using ApexPost.Application.Abstractions;

namespace ApexPost.UnitTests.Support;

/// <summary>Holds what the fake repositories staged until a commit, as the real unit of work does.</summary>
internal sealed class FakeUnitOfWork(UnitOfWorkSpy? spy = null) : IUnitOfWork
{
    private readonly UnitOfWorkSpy _spy = spy ?? new UnitOfWorkSpy();
    private readonly List<Func<CancellationToken, Task>> _staged = [];

    public int TransactionCount { get; private set; }

    public void Stage(Func<CancellationToken, Task> change) => _staged.Add(change);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        _spy.ThrowIfCommitMustFail();
        foreach (var change in _staged)
        {
            await change(cancellationToken);
        }

        _staged.Clear();
        _spy.Committed();
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        TransactionCount++;
        await work(cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }
}
