using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Domain.Pricing;

/// <summary>Prices a shipment to a zone; there is one strategy per service level.</summary>
public interface IShippingPricingStrategy
{
    Money Price(Shipment shipment, ShippingZone zone);
}
