using System.Globalization;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Domain.Transactions;

/// <summary>A priced shipment to a destination zone. Shipping is taxed at the standard rate.</summary>
public sealed record ShippingLineItem(Guid Id, Shipment Shipment, string ZoneCode, Money Price) : ILineItem
{
    private const string TrackingPrefix = "AP";
    private const string PlainGuidFormat = "N";
    private const int TrackingDigits = 10;

    public Guid Id { get; } = Guard.NotEmpty(Id, "Line id");

    public Shipment Shipment { get; } = Guard.Required(Shipment, nameof(Shipment));

    public string ZoneCode { get; } = Guard.NotBlank(ZoneCode, "Zone code", TextLimits.ZoneCode).ToUpperInvariant();

    public Money Price { get; } = Guard.Required(Price, nameof(Price)).IsPositive
        ? Price
        : throw new DomainException("A shipping price must be greater than zero.");

    public string Description => $"{Shipment.Level} {Shipment.Type} to zone {ZoneCode}";

    public Money Total => Price;

    public TaxCategory TaxCategory => TaxCategory.Standard;

    /// <summary>The number printed on the label, taken from the random part of the line id.</summary>
    public string TrackingNumber =>
        TrackingPrefix + Id.ToString(PlainGuidFormat, CultureInfo.InvariantCulture)[^TrackingDigits..]
            .ToUpperInvariant();
}
