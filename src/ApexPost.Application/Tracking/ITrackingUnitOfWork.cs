namespace ApexPost.Application.Tracking;

/// <summary>
/// Commits what the tracking repository staged, once per use case. Separate from the Week 3 unit of work because
/// tracking has its own database context. Failures arrive as <see cref="Exceptions.DuplicateKeyException"/> or
/// <see cref="Exceptions.ConcurrencyConflictException"/>.
/// </summary>
public interface ITrackingUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Forgets everything staged and loaded, so a failed use case can start again from the database.</summary>
    void DiscardChanges();
}
