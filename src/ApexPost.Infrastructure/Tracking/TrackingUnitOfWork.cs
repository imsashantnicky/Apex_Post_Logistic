using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Tracking;

/// <summary>
/// The one commit of a tracking use case. Before saving it raises the Version of every changed shipment, so a
/// parallel change of the same shipment fails with <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class TrackingUnitOfWork(TrackingDbContext database) : ITrackingUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in database.ChangeTracker.Entries<TrackedShipment>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(shipment => shipment.Version).CurrentValue = entry.Entity.Version + 1;
            }
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The shipment was changed by someone else.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException
        {
            Number: UniqueIndexViolation or UniqueConstraintViolation,
        })
        {
            throw new DuplicateKeyException("A shipment or scan with the same key already exists.", exception);
        }
    }

    public void DiscardChanges() => database.ChangeTracker.Clear();
}
