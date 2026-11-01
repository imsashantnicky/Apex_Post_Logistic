using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

/// <summary>What the customer sends: letter or parcel, service level, measures and recipient.</summary>
public sealed record Shipment(
    ShipmentType Type,
    ServiceLevel Level,
    Weight Weight,
    Dimensions Dimensions,
    Address Recipient)
{
    public ShipmentType Type { get; } = Guard.Defined(Type, nameof(Type));

    public ServiceLevel Level { get; } = Guard.Defined(Level, nameof(Level));

    public Weight Weight { get; } = Guard.Required(Weight, nameof(Weight));

    public Dimensions Dimensions { get; } = Guard.Required(Dimensions, nameof(Dimensions));

    public Address Recipient { get; } = Guard.Required(Recipient, nameof(Recipient));

    /// <summary>The heavier of the actual and the volumetric weight, in kilograms.</summary>
    public decimal ChargeableKg => Math.Max(Weight.Kilograms, Dimensions.VolumetricKg);
}
