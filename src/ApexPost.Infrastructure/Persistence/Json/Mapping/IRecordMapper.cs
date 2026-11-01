using ApexPost.Domain.Common;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

/// <summary>Translates between a domain entity and its stored record; the domain never sees a record.</summary>
public interface IRecordMapper<TEntity, TId, TRecord>
    where TEntity : Entity<TId>
    where TId : notnull
    where TRecord : IVersionedRecord
{
    TId IdOf(TRecord record);

    TRecord ToRecord(TEntity entity, int version);

    /// <summary>Rebuilds the entity; the domain constructors reject a record with invalid values.</summary>
    TEntity ToEntity(TRecord record);
}
