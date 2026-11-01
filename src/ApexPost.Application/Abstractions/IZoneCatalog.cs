using ApexPost.Domain.Shipping;

namespace ApexPost.Application.Abstractions;

/// <summary>Read-only shipping zone lookups; answered from a cache most of the time, hence ValueTask.</summary>
public interface IZoneCatalog
{
    ValueTask<ShippingZone?> FindAsync(string zoneCode, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ShippingZone>> ListAsync(CancellationToken cancellationToken);
}
