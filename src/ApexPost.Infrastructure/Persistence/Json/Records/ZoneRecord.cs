namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record ZoneRecord : IVersionedRecord
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required MoneyRecord BaseRate { get; init; }

    public required MoneyRecord PerKgRate { get; init; }

    public required int Version { get; init; }
}
