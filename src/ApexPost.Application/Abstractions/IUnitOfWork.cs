namespace ApexPost.Application.Abstractions;

/// <summary>
/// Commits what the repositories staged. One use case commits once, so its changes succeed or fail together. Failures
/// arrive as <see cref="Exceptions.ConcurrencyConflictException"/>, <see cref="Exceptions.DuplicateKeyException"/>
/// or <see cref="Exceptions.RecordInUseException"/>.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs work that cannot go through one save, such as a set-based update, in an explicit transaction, and saves
    /// what it staged before committing. The work may run again after a transient failure.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}
