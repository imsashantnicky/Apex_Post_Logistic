namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>A shipping line with the price that was quoted when it was added.</summary>
public sealed record ShippingLineRecord : LineItemRecord
{
    public required ShipmentRecord Shipment { get; init; }

    public required string ZoneCode { get; init; }

    public required MoneyRecord Price { get; init; }
}
