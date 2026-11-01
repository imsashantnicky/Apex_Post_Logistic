using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace ApexPost.Application.Services;

/// <summary>
/// Prices a shipment: looks up the zone in the cached catalogue and lets the pricing strategy of the service
/// level (chosen by pattern matching in <see cref="ShippingPricing"/>) calculate the price.
/// </summary>
public sealed class ShippingPricer(IZoneCatalog zones, IOptions<ShippingSettings> settings)
{
    private readonly ServiceLevelMultipliers _multipliers = settings.Value.ToMultipliers();

    public async ValueTask<ShippingLineItem> CreateLineAsync(
        Shipment shipment,
        string zoneCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        var zone = await zones.FindAsync(zoneCode, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Shipping zone {zoneCode} does not exist.");
        return ShippingLineItem.Create(shipment, zone, _multipliers);
    }
}
