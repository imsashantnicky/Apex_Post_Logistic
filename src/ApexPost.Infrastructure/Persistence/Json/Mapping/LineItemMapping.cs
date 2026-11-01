using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

/// <summary>Maps the three kinds of basket line with pattern matching on their type.</summary>
public static class LineItemMapping
{
    public static LineItemRecord ToRecord(ILineItem line) => line switch
    {
        RetailLineItem retail => new RetailLineRecord
        {
            Id = retail.Id,
            Sku = retail.Sku.Value,
            Name = retail.Name,
            UnitPrice = retail.UnitPrice.ToRecord(),
            Quantity = retail.Quantity,
            TaxCategory = retail.TaxCategory,
        },
        ShippingLineItem shipping => new ShippingLineRecord
        {
            Id = shipping.Id,
            Shipment = ToRecord(shipping.Shipment),
            ZoneCode = shipping.ZoneCode,
            Price = shipping.Price.ToRecord(),
        },
        MoneyOrderLineItem order => new MoneyOrderLineRecord
        {
            Id = order.Id,
            Amount = order.Amount.ToRecord(),
            Fee = order.Fee.ToRecord(),
            Sender = order.Sender,
            Recipient = order.Recipient,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(line), line?.GetType().Name, "Unknown line type."),
    };

    public static ILineItem ToLine(LineItemRecord record) => record switch
    {
        RetailLineRecord retail => new RetailLineItem(
            retail.Id,
            new Sku(retail.Sku),
            retail.Name,
            retail.UnitPrice.ToMoney(),
            retail.Quantity,
            retail.TaxCategory),
        ShippingLineRecord shipping => new ShippingLineItem(
            shipping.Id,
            ToShipment(shipping.Shipment),
            shipping.ZoneCode,
            shipping.Price.ToMoney()),
        MoneyOrderLineRecord order => new MoneyOrderLineItem(
            order.Id,
            order.Amount.ToMoney(),
            order.Fee.ToMoney(),
            order.Sender,
            order.Recipient),
        _ => throw new ArgumentOutOfRangeException(nameof(record), record?.GetType().Name, "Unknown line record."),
    };

    private static ShipmentRecord ToRecord(Shipment shipment) => new()
    {
        Type = shipment.Type,
        Level = shipment.Level,
        WeightGrams = shipment.Weight.Grams,
        LengthCm = shipment.Dimensions.LengthCm,
        WidthCm = shipment.Dimensions.WidthCm,
        HeightCm = shipment.Dimensions.HeightCm,
        Recipient = new AddressRecord
        {
            Name = shipment.Recipient.Name,
            Street = shipment.Recipient.Street,
            City = shipment.Recipient.City,
            PostalCode = shipment.Recipient.PostalCode,
            Country = shipment.Recipient.Country,
        },
    };

    private static Shipment ToShipment(ShipmentRecord record) =>
        new(
            record.Type,
            record.Level,
            new Weight(record.WeightGrams),
            new Dimensions(record.LengthCm, record.WidthCm, record.HeightCm),
            new Address
            {
                Name = record.Recipient.Name,
                Street = record.Recipient.Street,
                City = record.Recipient.City,
                PostalCode = record.Recipient.PostalCode,
                Country = record.Recipient.Country,
            });
}
