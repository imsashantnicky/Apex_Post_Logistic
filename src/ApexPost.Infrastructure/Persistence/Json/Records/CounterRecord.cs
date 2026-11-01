namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record CounterRecord
{
    public required Guid Id { get; init; }

    public required int Number { get; init; }
}
