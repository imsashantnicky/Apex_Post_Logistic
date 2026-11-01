using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Common;
using ApexPost.Infrastructure.Persistence.Json.Mapping;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>
/// Stores one kind of aggregate as a list of records in one JSON file. A new entity is stored as version 1;
/// an update or delete must name the stored version, otherwise <see cref="ConcurrencyException"/> is thrown
/// and the file stays as it was. A duplicate key or an invalid record raises <see cref="DataFileException"/>.
/// </summary>
public sealed class JsonRepository<TEntity, TId, TRecord>(
    JsonFileStore files,
    string fileName,
    IRecordMapper<TEntity, TId, TRecord> mapper) : IRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
    where TRecord : IVersionedRecord
{
    private const int FirstVersion = 1;

    public async Task<TEntity?> GetAsync(TId id, CancellationToken cancellationToken)
    {
        var records = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var index = IndexOf(records, id);
        return index < 0 ? null : ToEntity(records[index]);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken) =>
        [.. (await LoadAsync(cancellationToken).ConfigureAwait(false)).Select(ToEntity)];

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specification);
        var all = await ListAsync(cancellationToken).ConfigureAwait(false);
        return [.. all.Where(specification.IsSatisfiedBy)];
    }

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return ChangeAsync(
            records =>
            {
                if (IndexOf(records, entity.Id) >= 0)
                {
                    throw new ConcurrencyException($"{typeof(TEntity).Name} {entity.Id} already exists.");
                }

                records.Add(mapper.ToRecord(entity, FirstVersion));
            },
            cancellationToken);
    }

    public Task UpdateAsync(TEntity entity, int expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return ChangeAsync(
            records => records[StoredIndex(records, entity.Id, expectedVersion)] =
                mapper.ToRecord(entity, expectedVersion + 1),
            cancellationToken);
    }

    public Task DeleteAsync(TId id, int expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(records => records.RemoveAt(StoredIndex(records, id, expectedVersion)), cancellationToken);

    private async Task<List<TRecord>> LoadAsync(CancellationToken cancellationToken)
    {
        var records = await files.ReadAsync<TRecord>(fileName, cancellationToken).ConfigureAwait(false);
        EnsureUniqueKeys(records);
        return records;
    }

    private Task ChangeAsync(Action<List<TRecord>> change, CancellationToken cancellationToken) =>
        files.ChangeAsync<TRecord>(
            fileName,
            records =>
            {
                EnsureUniqueKeys(records);
                EnsureValid(records);
                change(records);
            },
            cancellationToken);

    /// <summary>The position of the stored record, if it still has the version the caller loaded.</summary>
    private int StoredIndex(List<TRecord> records, TId id, int expectedVersion)
    {
        var index = IndexOf(records, id);
        if (index < 0 || records[index].Version != expectedVersion)
        {
            throw new ConcurrencyException(
                $"{typeof(TEntity).Name} {id} was changed or removed by someone else. Reload it and try again.");
        }

        return index;
    }

    private int IndexOf(List<TRecord> records, TId id) =>
        records.FindIndex(record => EqualityComparer<TId>.Default.Equals(KeyOf(record), id));

    private void EnsureUniqueKeys(List<TRecord> records)
    {
        var duplicate = records.GroupBy(KeyOf).FirstOrDefault(group => group.Skip(1).Any());
        if (duplicate is not null)
        {
            throw new DataFileException($"Data file '{fileName}' holds key {duplicate.Key} more than once.");
        }
    }

    /// <summary>A write keeps the other records, so an invalid one must stop it, not pass through it.</summary>
    private void EnsureValid(List<TRecord> records) => records.ForEach(record => ToEntity(record));

    private TId KeyOf(TRecord record) => Mapped(() => mapper.IdOf(record));

    private TEntity ToEntity(TRecord record) => Mapped(() => mapper.ToEntity(record));

    private T Mapped<T>(Func<T> map)
    {
        try
        {
            return map();
        }
        catch (DomainException exception)
        {
            throw new DataFileException(
                $"Data file '{fileName}' holds an invalid record: {exception.Message}",
                exception);
        }
    }
}
