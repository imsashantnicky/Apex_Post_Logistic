using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Shipping;
using Microsoft.Extensions.Options;

namespace ApexPost.Infrastructure.Caching;

/// <summary>
/// Products and shipping zones are read on almost every counter action but change rarely, so both lists are
/// cached for <see cref="CatalogSettings.CacheSeconds"/>. Register as a singleton.
/// </summary>
public sealed class CachedCatalog : IProductCatalog, IZoneCatalog, IDisposable
{
    private readonly TimedCache<Product> _products;
    private readonly TimedCache<ShippingZone> _zones;

    public CachedCatalog(
        IRepository<Product, Sku> products,
        IRepository<ShippingZone, string> zones,
        IClock clock,
        IOptions<CatalogSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(settings);
        var timeToLive = TimeSpan.FromSeconds(settings.Value.CacheSeconds);
        _products = new TimedCache<Product>(products.ListAsync, clock, timeToLive);
        _zones = new TimedCache<ShippingZone>(zones.ListAsync, clock, timeToLive);
    }

    /// <summary>How often a list came from memory, for the measurements in DESIGN.md.</summary>
    public int Hits => _products.Hits + _zones.Hits;

    /// <summary>How often a list was read from storage.</summary>
    public int Loads => _products.Loads + _zones.Loads;

    public async ValueTask<Product?> FindAsync(Sku sku, CancellationToken cancellationToken)
    {
        var all = await _products.GetAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(product => product.Sku == sku);
    }

    ValueTask<IReadOnlyList<Product>> IProductCatalog.ListAsync(CancellationToken cancellationToken) =>
        _products.GetAsync(cancellationToken);

    public async ValueTask<ShippingZone?> FindAsync(string zoneCode, CancellationToken cancellationToken)
    {
        var all = await _zones.GetAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(zone => zone.Code.Equals(zoneCode?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    ValueTask<IReadOnlyList<ShippingZone>> IZoneCatalog.ListAsync(CancellationToken cancellationToken) =>
        _zones.GetAsync(cancellationToken);

    public void Dispose()
    {
        _products.Dispose();
        _zones.Dispose();
    }
}
