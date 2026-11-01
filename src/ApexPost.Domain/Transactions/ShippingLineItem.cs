using System.Globalization;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Shipping;

namespace ApexPost.Domain.Transactions;

/// <summary>A priced shipment to a destination zone. Shipping is taxed at the standard rate.</summary>
public sealed record ShippingLineItem : TransactionLine
{
    private const string TrackingPrefix = "AP";
    private const string PlainGuidFormat = "N";
    private const int TrackingDigits = 10;

    public ShippingLineItem(Guid id, Shipment shipment, string zoneCode, Money price)
        : this(id, zoneCode, price)
    {
        Shipment = Guard.Required(shipment, nameof(Shipment));
    }

    // Used by EF Core: the shipment is a separate row and is attached after the line is read.
    private ShippingLineItem(Guid id, string zoneCode, Money price)
        : base(id)
    {
        ZoneCode = Guard.NotBlank(zoneCode, "Zone code", TextLimits.ZoneCode).ToUpperInvariant();
        Price = Guard.Required(price, nameof(Price)).IsPositive
            ? price
            : throw new DomainException("A shipping price must be greater than zero.");
        Shipment = null!;
    }

    public Shipment Shipment { get; }

    public string ZoneCode { get; }

    public Money Price { get; }

    public override string Description => $"{Shipment.Level} {Shipment.Type} to zone {ZoneCode}";

    public override Money Total => Price;

    public override TaxCategory TaxCategory => TaxCategory.Standard;

    /// <summary>The number printed on the label, taken from the random part of the line id.</summary>
    public string TrackingNumber =>
        TrackingPrefix + Id.ToString(PlainGuidFormat, CultureInfo.InvariantCulture)[^TrackingDigits..]
            .ToUpperInvariant();

    public static ShippingLineItem Create(Shipment shipment, ShippingZone zone, ServiceLevelMultipliers multipliers) =>
        new(Guid.CreateVersion7(), shipment, zone.Code, ShippingPricing.Quote(shipment, zone, multipliers));
}
