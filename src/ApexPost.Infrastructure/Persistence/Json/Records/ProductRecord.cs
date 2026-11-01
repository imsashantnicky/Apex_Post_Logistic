using ApexPost.Domain.Catalogue;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record ProductRecord : IVersionedRecord
{
    public required string Sku { get; init; }

    public required string Name { get; init; }

    public required ProductCategory Category { get; init; }

    public required MoneyRecord UnitPrice { get; init; }

    public required TaxCategory TaxCategory { get; init; }

    public required int Version { get; init; }
}
