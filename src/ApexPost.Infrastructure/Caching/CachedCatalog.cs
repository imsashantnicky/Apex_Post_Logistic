using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
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
        : this(RequiredLoader(products), RequiredLoader(zones), clock, settings)
    {
    }

    public CachedCatalog(SqlCatalogLoader loader, IClock clock, IOptions<CatalogSettings> settings)
        : this(RequiredLoader(loader).ProductsAsync, loader.ZonesAsync, clock, settings)
    {
    }

    private CachedCatalog(
        Func<CancellationToken, Task<IReadOnlyList<Product>>> products,
        Func<CancellationToken, Task<IReadOnlyList<ShippingZone>>> zones,
        IClock clock,
        IOptions<CatalogSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var timeToLive = TimeSpan.FromSeconds(settings.Value.CacheSeconds);
        _products = new TimedCache<Product>(products, clock, timeToLive);
        _zones = new TimedCache<ShippingZone>(zones, clock, timeToLive);
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

    public void Refresh() => _products.Clear();

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

    private static Func<CancellationToken, Task<IReadOnlyList<TEntity>>> RequiredLoader<TEntity, TId>(
        IRepository<TEntity, TId> repository)
        where TEntity : Entity<TId>
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);
        return repository.ListAsync;
    }

    private static SqlCatalogLoader RequiredLoader(SqlCatalogLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        return loader;
    }
}
