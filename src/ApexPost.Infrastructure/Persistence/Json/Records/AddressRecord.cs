namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record AddressRecord
{
    public required string Name { get; init; }

    public required string Street { get; init; }

    public required string City { get; init; }

    public required string PostalCode { get; init; }

    public required string Country { get; init; }
}
