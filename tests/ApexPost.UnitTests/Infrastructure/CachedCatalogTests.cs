using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.Infrastructure.Caching;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Options;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class CachedCatalogTests : IDisposable
{
    private const int CacheSeconds = 60;

    private readonly InMemoryRepository<Product, Sku> _products = new(EntityCopies.Copy);
    private readonly InMemoryRepository<ShippingZone, string> _zones = new(EntityCopies.Copy);
    private readonly FixedClock _clock = new();
    private readonly CachedCatalog _catalog;

    public CachedCatalogTests()
    {
        _products.Seed(TestData.CreateProduct());
        _zones.Seed(new ShippingZone("Z01", "Local", new Money(3.50m), new Money(0.80m)));
        _catalog = new CachedCatalog(
            _products,
            _zones,
            _clock,
            Options.Create(new CatalogSettings { CacheSeconds = CacheSeconds }));
    }

    private IProductCatalog Products => _catalog;

    private IZoneCatalog Zones => _catalog;

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task Refresh_makes_the_next_read_load_the_products_again()
    {
        var sku = new Sku(TestData.FirstSku);
        await Products.FindAsync(sku, Ct);
        await Products.FindAsync(sku, Ct);

        Products.Refresh();
        await Products.FindAsync(sku, Ct);

        Assert.Equal(2, _catalog.Loads);
    }

    [Fact]
    public async Task Repeated_reads_load_once()
    {
        const int Reads = 50;
        var sku = new Sku(TestData.FirstSku);

        for (var read = 0; read < Reads; read++)
        {
            Assert.NotNull(await Products.FindAsync(sku, Ct));
        }

        Assert.Equal(1, _products.ListCount);
        Assert.Equal(1, _catalog.Loads);
        Assert.Equal(Reads - 1, _catalog.Hits);
    }

    [Fact]
    public async Task Parallel_misses_load_once()
    {
        const int Readers = 20;

        await Task.WhenAll(Enumerable.Range(0, Readers).Select(_ => Products.ListAsync(Ct).AsTask()));

        Assert.Equal(1, _products.ListCount);
    }

    [Fact]
    public async Task A_hit_completes_synchronously()
    {
        await Products.ListAsync(Ct);

        var hit = Products.ListAsync(Ct);

        Assert.True(hit.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Entry_at_the_end_of_its_lifetime_is_loaded_again()
    {
        await Products.ListAsync(Ct);
        _clock.UtcNow = _clock.UtcNow.AddSeconds(CacheSeconds - 1);
        await Products.ListAsync(Ct);
        Assert.Equal(1, _products.ListCount);

        _clock.UtcNow = _clock.UtcNow.AddSeconds(1);
        await Products.ListAsync(Ct);

        Assert.Equal(2, _products.ListCount);
    }

    [Fact]
    public async Task Products_and_zones_are_cached_apart()
    {
        await Products.ListAsync(Ct);
        await Zones.ListAsync(Ct);

        Assert.Equal(1, _products.ListCount);
        Assert.Equal(1, _zones.ListCount);
        Assert.Equal(2, _catalog.Loads);
    }

    [Theory]
    [InlineData("Z01", true)]
    [InlineData(" z01 ", true)]
    [InlineData("Z99", false)]
    public async Task Zone_lookup_ignores_case_and_spaces(string code, bool found)
    {
        Assert.Equal(found, await Zones.FindAsync(code, Ct) is not null);
    }

    [Fact]
    public async Task A_failed_load_is_not_cached()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Products.ListAsync(cancelled.Token).AsTask());

        Assert.NotEmpty(await Products.ListAsync(Ct));
        Assert.Equal(1, _catalog.Loads);
    }
}
