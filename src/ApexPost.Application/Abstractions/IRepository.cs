using ApexPost.Application.Specifications;
using ApexPost.Domain.Common;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Stores one kind of aggregate. Every stored entity has a version: an update must name the version it was
/// loaded with, otherwise <see cref="Exceptions.ConcurrencyException"/> is thrown. A new entity is stored as
/// version 1, so reload it before updating it.
/// </summary>
public interface IRepository<TEntity, in TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    Task<TEntity?> GetAsync(TId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken);

    Task UpdateAsync(TEntity entity, int expectedVersion, CancellationToken cancellationToken);

    Task DeleteAsync(TId id, int expectedVersion, CancellationToken cancellationToken);
}
