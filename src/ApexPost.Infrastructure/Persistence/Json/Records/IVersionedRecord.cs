namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>A stored row with a version that grows by one on every update (later an EF concurrency token).</summary>
public interface IVersionedRecord
{
    int Version { get; }
}
