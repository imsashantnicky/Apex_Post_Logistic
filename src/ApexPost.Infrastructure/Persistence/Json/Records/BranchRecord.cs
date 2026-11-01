namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record BranchRecord : IVersionedRecord
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Region { get; init; }

    public required TimeOnly OpensAt { get; init; }

    public required TimeOnly ClosesAt { get; init; }

    public required bool IsActive { get; init; }

    public required IReadOnlyList<CounterRecord> Counters { get; init; }

    public required int Version { get; init; }
}
