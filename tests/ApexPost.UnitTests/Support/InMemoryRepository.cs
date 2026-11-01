using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Common;

namespace ApexPost.UnitTests.Support;

/// <summary>
/// A thread-safe repository fake with the same version rules as the JSON store. Each call yields first, so
/// parallel callers really interleave.
/// </summary>
internal sealed class InMemoryRepository<TEntity, TId>(Func<TEntity, int, TEntity> copy) : IRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    private readonly Dictionary<TId, TEntity> _items = [];
    private readonly Lock _lock = new();

    private int _listCount;

    public int UpdateCount { get; private set; }

    public int ListCount => Volatile.Read(ref _listCount);

    public async Task<TEntity?> GetAsync(TId id, CancellationToken cancellationToken)
    {
        await Task.Yield();
        lock (_lock)
        {
            return _items.TryGetValue(id, out var item) ? copy(item, item.Version) : null;
        }
    }

    public Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken) =>
        ListAsync(new PredicateSpecification<TEntity>(_ => true), cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        Interlocked.Increment(ref _listCount);
        lock (_lock)
        {
            return [.. _items.Values.Where(specification.IsSatisfiedBy).Select(item => copy(item, item.Version))];
        }
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            if (!_items.TryAdd(entity.Id, copy(entity, 1)))
            {
                throw new ConcurrencyException($"{entity.Id} already exists.");
            }
        }
    }

    public async Task UpdateAsync(TEntity entity, int expectedVersion, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            if (!_items.TryGetValue(entity.Id, out var stored) || stored.Version != expectedVersion)
            {
                throw new ConcurrencyException($"{entity.Id} was changed by someone else.");
            }

            _items[entity.Id] = copy(entity, expectedVersion + 1);
            UpdateCount++;
        }
    }

    public async Task DeleteAsync(TId id, int expectedVersion, CancellationToken cancellationToken)
    {
        await Task.Yield();
        lock (_lock)
        {
            if (!_items.TryGetValue(id, out var stored) || stored.Version != expectedVersion)
            {
                throw new ConcurrencyException($"{id} was changed by someone else.");
            }

            _items.Remove(id);
        }
    }

    /// <summary>Stores an entity directly, for test setup.</summary>
    public TEntity Seed(TEntity entity)
    {
        lock (_lock)
        {
            _items[entity.Id] = copy(entity, 1);
        }

        return entity;
    }
}
