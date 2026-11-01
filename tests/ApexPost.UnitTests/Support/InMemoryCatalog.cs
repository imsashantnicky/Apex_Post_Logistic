using ApexPost.Application.Abstractions;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Shipping;

namespace ApexPost.UnitTests.Support;

internal sealed class InMemoryCatalog : IProductCatalog, IZoneCatalog
{
    private readonly List<Product> _products = [];
    private readonly List<ShippingZone> _zones = [];

    public void Add(Product product) => _products.Add(product);

    public void Add(ShippingZone zone) => _zones.Add(zone);

    public ValueTask<Product?> FindAsync(Sku sku, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_products.Find(product => product.Sku == sku));

    ValueTask<IReadOnlyList<Product>> IProductCatalog.ListAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Product>>(_products);

    public ValueTask<ShippingZone?> FindAsync(string zoneCode, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_zones.Find(zone => zone.Code.Equals(zoneCode, StringComparison.OrdinalIgnoreCase)));

    ValueTask<IReadOnlyList<ShippingZone>> IZoneCatalog.ListAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ShippingZone>>(_zones);
}
