using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories;

/// <summary>
/// The commit of one use case. One SaveChanges is one database transaction, so what a repository staged is stored
/// completely or not at all. Database failures become the Application exceptions the web layer answers with 409.
/// </summary>
public sealed class UnitOfWork(ApexPostDbContext database) : IUnitOfWork
{
    private const int ForeignKeyViolation = 547;
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The record was changed by someone else.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException
        {
            Number: UniqueIndexViolation or UniqueConstraintViolation,
        })
        {
            throw new DuplicateKeyException("A record with the same key already exists.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException
        {
            Number: ForeignKeyViolation,
        })
        {
            throw new RecordInUseException("Stored history still refers to this record.", exception);
        }
    }

    /// <summary>
    /// The execution strategy replays the whole unit after a transient failure, so a replay starts from a clean
    /// tracker and a user transaction never outlives one attempt.
    /// </summary>
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        var isReplay = false;
        return database.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                if (isReplay)
                {
                    database.ChangeTracker.Clear();
                }

                isReplay = true;
                var transaction = await database.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                await using (transaction.ConfigureAwait(false))
                {
                    await work(token).ConfigureAwait(false);
                    await SaveChangesAsync(token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            },
            cancellationToken);
    }
}
