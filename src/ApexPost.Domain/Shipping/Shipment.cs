using ApexPost.Domain.Common;
using ApexPost.Domain.Rules;

namespace ApexPost.Domain.Shipping;

/// <summary>
/// What the customer sends: letter or parcel, service level, measures and recipient.
/// A shipment over the weight limit of its type and service level cannot be created.
/// </summary>
public sealed record Shipment
{
    public Shipment(ShipmentType type, ServiceLevel level, Weight weight, Dimensions dimensions, Address recipient)
        : this(type, level, weight)
    {
        Dimensions = Guard.Required(dimensions, nameof(Dimensions));
        Recipient = Guard.Required(recipient, nameof(Recipient));
    }

    // Used by EF Core: dimensions and recipient are owned types and are attached after the scalar values.
    private Shipment(ShipmentType type, ServiceLevel level, Weight weight)
    {
        Type = Guard.Defined(type, nameof(Type));
        Level = Guard.Defined(level, nameof(Level));
        Weight = WeightLimitRule.Ensure(Type, Level, Guard.Required(weight, nameof(Weight)));
        Dimensions = null!;
        Recipient = null!;
    }

    public ShipmentType Type { get; }

    public ServiceLevel Level { get; }

    public Weight Weight { get; }

    public Dimensions Dimensions { get; }

    public Address Recipient { get; }

    /// <summary>The heavier of the actual and the volumetric weight, in kilograms.</summary>
    public decimal ChargeableKg => ChargeableKgOf(Weight, Dimensions);

    public static decimal ChargeableKgOf(Weight weight, Dimensions dimensions) =>
        Math.Max(
            Guard.Required(weight, nameof(weight)).Kilograms,
            Guard.Required(dimensions, nameof(dimensions)).VolumetricKg);
}
