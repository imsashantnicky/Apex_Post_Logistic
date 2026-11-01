using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Domain.Rules;

/// <summary>The maximum actual weight per shipment type and service level; heavier items are rejected.</summary>
public static class WeightLimitRule
{
    public const decimal LetterMaxKg = 2m;
    public const decimal ParcelStandardMaxKg = 30m;
    public const decimal ParcelExpressMaxKg = 20m;
    public const decimal ParcelRegisteredMaxKg = 10m;

    public static Weight MaximumFor(ShipmentType type, ServiceLevel level) =>
        Weight.FromKilograms((type, level) switch
        {
            (ShipmentType.Letter, _) => LetterMaxKg,
            (ShipmentType.Parcel, ServiceLevel.Standard) => ParcelStandardMaxKg,
            (ShipmentType.Parcel, ServiceLevel.Express) => ParcelExpressMaxKg,
            (ShipmentType.Parcel, ServiceLevel.Registered) => ParcelRegisteredMaxKg,
            _ => throw new DomainException($"There is no weight limit for a {level} {type}."),
        });

    public static bool Allows(ShipmentType type, ServiceLevel level, Weight weight) =>
        weight <= MaximumFor(type, level);

    public static Weight Ensure(ShipmentType type, ServiceLevel level, Weight weight) =>
        Allows(type, level, weight)
            ? weight
            : throw new DomainException(
                $"A {level} {type} can weigh at most {MaximumFor(type, level)}; this one weighs {weight}.");
}
