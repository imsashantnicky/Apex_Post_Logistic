using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Domain.Pricing;

/// <summary>
/// Chooses the pricing strategy for a service level. Every strategy uses
/// price = (zone base rate + zone rate per kg * chargeable kg) * service level multiplier.
/// </summary>
public static class ShippingPricing
{
    public static IShippingPricingStrategy For(ServiceLevel level, ServiceLevelMultipliers multipliers)
    {
        Guard.Required(multipliers, nameof(multipliers));
        return level switch
        {
            ServiceLevel.Standard => new StandardShippingPricing(multipliers.Standard),
            ServiceLevel.Express => new ExpressShippingPricing(multipliers.Express),
            ServiceLevel.Registered => new RegisteredShippingPricing(multipliers.Registered),
            _ => throw new DomainException($"Service level '{level}' is not valid."),
        };
    }

    public static Money Quote(Shipment shipment, ShippingZone zone, ServiceLevelMultipliers multipliers) =>
        For(shipment.Level, multipliers).Price(shipment, zone);

    internal static Money Calculate(Shipment shipment, ShippingZone zone, decimal multiplier) =>
        new(
            (zone.BaseRate.Amount + (zone.PerKgRate.Amount * shipment.ChargeableKg)) * multiplier,
            zone.BaseRate.Currency);
}
