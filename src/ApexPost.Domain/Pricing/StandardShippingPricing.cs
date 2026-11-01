using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Domain.Pricing;

public sealed class StandardShippingPricing(decimal multiplier) : IShippingPricingStrategy
{
    public Money Price(Shipment shipment, ShippingZone zone) => ShippingPricing.Calculate(shipment, zone, multiplier);
}
