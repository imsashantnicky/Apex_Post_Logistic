using ApexPost.Domain.Catalogue;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record RetailLineRecord : LineItemRecord
{
    public required string Sku { get; init; }

    public required string Name { get; init; }

    public required MoneyRecord UnitPrice { get; init; }

    public required int Quantity { get; init; }

    public required TaxCategory TaxCategory { get; init; }
}
