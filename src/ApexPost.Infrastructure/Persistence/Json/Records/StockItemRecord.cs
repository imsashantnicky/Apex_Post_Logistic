namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>Stock of one product in one branch; Branch and Sku together are the key.</summary>
public sealed record StockItemRecord : IVersionedRecord
{
    public required string Branch { get; init; }

    public required string Sku { get; init; }

    public required int Quantity { get; init; }

    public required int Version { get; init; }
}
