using System.Globalization;
using ApexPost.Application.Abstractions;
using ApexPost.Domain.Catalogue;
using ApexPost.Infrastructure.Caching;
using ApexPost.Infrastructure.Seeding;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.DependencyInjection;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Measurements;

[Trait("Category", "Measurement")]
[Collection(nameof(SerialMeasurements))]
public sealed class CatalogCacheMeasurements(ITestOutputHelper output)
{
    private const int Repeats = 7;
    private const int FileReads = 200;
    private const int CachedReads = 20_000;
    private const int AllocationReads = 100_000;

    [Fact]
    public async Task Cached_lookup_against_a_json_file_read()
    {
        using var folder = new TempDataFolder();
        await using var host = await SeededHostAsync(folder);
        var repository = host.GetRequiredService<IRepository<Product, Sku>>();
        var catalog = host.GetRequiredService<IProductCatalog>();
        var counters = host.GetRequiredService<CachedCatalog>();
        var sku = new Sku(TestData.FirstSku);

        var fromFile = await MeasuredTime.OfAsync(Repeats, () => ReadFromFileAsync(repository, sku));
        var fromCache = await MeasuredTime.OfAsync(Repeats, () => ReadFromCacheAsync(catalog, sku));

        var filePerRead = fromFile.MedianMs * 1000 / FileReads;
        var cachePerRead = fromCache.MedianMs * 1000 / CachedReads;
        Report($"JSON file, {FileReads} lookups: {fromFile} = {filePerRead:F2} us per lookup");
        Report($"Cache, {CachedReads} lookups: {fromCache} = {cachePerRead:F3} us per lookup");
        Report($"Cache hits {counters.Hits}, loads {counters.Loads}; speed-up x{filePerRead / cachePerRead:F0}");
        Assert.True(cachePerRead < filePerRead);
    }

    [Fact]
    public async Task ValueTask_hit_against_a_Task_hit()
    {
        using var folder = new TempDataFolder();
        await using var host = await SeededHostAsync(folder);
        var catalog = host.GetRequiredService<IProductCatalog>();
        await catalog.ListAsync(Ct);

        var asValueTask = await AllocatedBytesPerReadAsync(async () => await catalog.ListAsync(Ct));
        var asTask = await AllocatedBytesPerReadAsync(() => catalog.ListAsync(Ct).AsTask());

        Report($"Allocated per cache hit, {AllocationReads} reads: ValueTask {asValueTask:F1} B, Task {asTask:F1} B");
        Assert.True(asValueTask < asTask);
    }

    private static async Task ReadFromFileAsync(IRepository<Product, Sku> repository, Sku sku)
    {
        for (var read = 0; read < FileReads; read++)
        {
            await repository.GetAsync(sku, Ct);
        }
    }

    private static async Task ReadFromCacheAsync(IProductCatalog catalog, Sku sku)
    {
        for (var read = 0; read < CachedReads; read++)
        {
            await catalog.FindAsync(sku, Ct);
        }
    }

    private static async Task<double> AllocatedBytesPerReadAsync(Func<Task> read)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var count = 0; count < AllocationReads; count++)
        {
            await read();
        }

        return (double)(GC.GetAllocatedBytesForCurrentThread() - before) / AllocationReads;
    }

    private static async Task<ServiceProvider> SeededHostAsync(TempDataFolder folder)
    {
        var host = InfrastructureHost.Build(folder.Location, folder.Location);
        await host.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(Ct);
        return host;
    }

    private void Report(FormattableString line) => output.WriteLine(line.ToString(CultureInfo.InvariantCulture));
}
