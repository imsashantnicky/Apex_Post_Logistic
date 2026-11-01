using ApexPost.Application.Abstractions;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.UnitTests.Support;

internal sealed class InMemoryCatalog : IProductCatalog, IZoneCatalog, IProductRepository
{
    private readonly List<Product> _products = [];
    private readonly List<ShippingZone> _zones = [];

    public int RefreshCount { get; private set; }

    public void Add(Product product) => _products.Add(product);

    public void Add(ShippingZone zone) => _zones.Add(zone);

    public ValueTask<Product?> FindAsync(Sku sku, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_products.Find(product => product.Sku == sku));

    ValueTask<IReadOnlyList<Product>> IProductCatalog.ListAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Product>>(_products);

    public void Refresh() => RefreshCount++;

    public ValueTask<ShippingZone?> FindAsync(string zoneCode, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_zones.Find(zone => zone.Code.Equals(zoneCode, StringComparison.OrdinalIgnoreCase)));

    ValueTask<IReadOnlyList<ShippingZone>> IZoneCatalog.ListAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ShippingZone>>(_zones);

    public Task<int> RevisePricesAsync(
        ProductCategory? category,
        decimal percent,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var matching = _products.Where(product => category is null || product.Category == category).ToList();
        foreach (var product in matching)
        {
            var revised = new Product(
                product.Sku,
                product.Name,
                product.Category,
                new Money(product.UnitPrice.Amount * (1 + (percent / 100m))),
                product.TaxCategory);
            _products[_products.IndexOf(product)] = revised;
        }

        return Task.FromResult(matching.Count);
    }
}
