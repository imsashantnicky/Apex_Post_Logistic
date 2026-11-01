using ApexPost.Application.Validation;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Application.DTOs;

/// <summary>A letter or parcel to price and add to the basket.</summary>
public sealed record AddShippingCommand
{
    public ShipmentType Type { get; init; } = ShipmentType.Parcel;

    public ServiceLevel Level { get; init; } = ServiceLevel.Standard;

    [PositiveNumber]
    public decimal WeightKg { get; init; }

    [PositiveNumber]
    public decimal LengthCm { get; init; }

    [PositiveNumber]
    public decimal WidthCm { get; init; }

    [PositiveNumber]
    public decimal HeightCm { get; init; }

    [NotBlank]
    [Pattern(InputPatterns.ZoneCode, InputPatterns.ZoneCodeExample)]
    public string ZoneCode { get; init; } = string.Empty;

    [NotBlank]
    [TextLength(TextLimits.Name)]
    public string RecipientName { get; init; } = string.Empty;

    [NotBlank]
    [TextLength(TextLimits.AddressLine)]
    public string Street { get; init; } = string.Empty;

    [NotBlank]
    [TextLength(TextLimits.Name)]
    public string City { get; init; } = string.Empty;

    [NotBlank]
    [TextLength(TextLimits.PostalCode)]
    public string PostalCode { get; init; } = string.Empty;

    [NotBlank]
    [TextLength(TextLimits.Name)]
    public string Country { get; init; } = string.Empty;
}
