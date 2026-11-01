using ApexPost.Application.Exceptions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Infrastructure.Persistence.Json.Mapping;
using ApexPost.Infrastructure.Persistence.Json.Records;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class JsonRepositoryTests : IDisposable
{
    private const string Sku = TestData.FirstSku;

    private readonly TempDataFolder _folder = new();

    private string StockFile => _folder.PathOf(DataFiles.Stock);

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Data_survives_a_restart()
    {
        await OpenStock().AddAsync(TestData.CreateStock(Sku, 7), Ct);

        var reloaded = await OpenStock().GetAsync(Key(Sku), Ct);

        Assert.NotNull(reloaded);
        Assert.Equal(7, reloaded.Quantity);
        Assert.Equal(1, reloaded.Version);
    }

    [Fact]
    public async Task Unknown_id_returns_null()
    {
        Assert.Null(await OpenStock().GetAsync(Key(Sku), Ct));
    }

    [Fact]
    public async Task Specification_filters_the_list()
    {
        var repository = OpenStock();
        await repository.AddAsync(TestData.CreateStock(TestData.FirstSku, 2), Ct);
        await repository.AddAsync(TestData.CreateStock(TestData.SecondSku, 9), Ct);

        var low = await repository.ListAsync(new PredicateSpecification<StockItem>(item => item.Quantity < 5), Ct);

        Assert.Equal(TestData.FirstSku, Assert.Single(low).Sku.Value);
    }

    [Fact]
    public async Task Update_with_the_loaded_version_raises_the_version()
    {
        var repository = OpenStock();
        var loaded = await AddAndLoadAsync(repository, quantity: 7);
        loaded.Add(3);

        await repository.UpdateAsync(loaded, loaded.Version, Ct);

        var reloaded = await repository.GetAsync(Key(Sku), Ct);
        Assert.Equal(10, reloaded!.Quantity);
        Assert.Equal(2, reloaded.Version);
        Assert.Contains("\"version\": 2", await File.ReadAllTextAsync(StockFile, Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_with_a_stale_version_is_rejected_and_changes_nothing()
    {
        var repository = OpenStock();
        var first = await AddAndLoadAsync(repository, quantity: 7);
        var second = await repository.GetAsync(Key(Sku), Ct);
        first.Add(1);
        await repository.UpdateAsync(first, first.Version, Ct);
        second!.Add(5);

        await Assert.ThrowsAsync<ConcurrencyException>(() => repository.UpdateAsync(second, second.Version, Ct));

        Assert.Equal(8, (await repository.GetAsync(Key(Sku), Ct))!.Quantity);
    }

    [Fact]
    public async Task Delete_with_a_stale_version_is_rejected()
    {
        var repository = OpenStock();
        var loaded = await AddAndLoadAsync(repository, quantity: 7);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            repository.DeleteAsync(Key(Sku), loaded.Version + 1, Ct));
        await repository.DeleteAsync(Key(Sku), loaded.Version, Ct);

        Assert.Null(await repository.GetAsync(Key(Sku), Ct));
    }

    [Fact]
    public async Task Adding_an_existing_id_is_rejected()
    {
        var repository = OpenStock();
        await repository.AddAsync(TestData.CreateStock(Sku, 7), Ct);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            repository.AddAsync(TestData.CreateStock(Sku, 1), Ct));
    }

    [Fact]
    public async Task Parallel_writers_lose_no_record()
    {
        const int Writers = 40;
        var repository = OpenStock();
        var writes = Enumerable.Range(1, Writers).Select(number =>
            repository.AddAsync(TestData.CreateStock(SkuNumber(number), number), Ct));

        await Task.WhenAll(writes);

        Assert.Equal(Writers, (await OpenStock().ListAsync(Ct)).Count);
        Assert.Empty(_folder.LeftoverTempFiles());
    }

    [Fact]
    public async Task Parallel_updates_of_one_version_have_exactly_one_winner()
    {
        const int Writers = 12;
        var repository = OpenStock();
        var loaded = await AddAndLoadAsync(repository, quantity: 7);
        var writes = Enumerable.Range(0, Writers).Select(_ => repository.UpdateAsync(loaded, loaded.Version, Ct));

        var outcomes = await Task.WhenAll(writes.Select(WonAsync));

        Assert.Equal(1, outcomes.Count(won => won));
        Assert.Equal(2, (await repository.GetAsync(Key(Sku), Ct))!.Version);
    }

    [Fact]
    public async Task Write_with_a_cancelled_token_leaves_the_file_as_it_was()
    {
        var repository = OpenStock();
        await repository.AddAsync(TestData.CreateStock(Sku, 7), Ct);
        var before = await File.ReadAllTextAsync(StockFile, Ct);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.AddAsync(TestData.CreateStock(TestData.SecondSku, 1), new CancellationToken(canceled: true)));

        Assert.Equal(before, await File.ReadAllTextAsync(StockFile, Ct));
    }

    [Fact]
    public async Task Write_cancelled_after_the_change_leaves_no_partial_file()
    {
        var store = _folder.OpenStore();
        var repository = OpenStock(store);
        await repository.AddAsync(TestData.CreateStock(Sku, 7), Ct);
        var before = await File.ReadAllTextAsync(StockFile, Ct);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.ChangeAsync<StockItemRecord>(
                DataFiles.Stock,
                records =>
                {
                    records.Clear();
                    cancellation.Cancel();
                },
                cancellation.Token));

        Assert.Equal(before, await File.ReadAllTextAsync(StockFile, Ct));
        Assert.Empty(_folder.LeftoverTempFiles());
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""[{ "branch": "BR001", "sku": "PKG-0001" }]""")]
    [InlineData("""[{ "branch": "BR001", "sku": "PKG-0001", "quantity": -1, "version": 1 }]""")]
    public async Task Bad_file_is_reported_and_never_overwritten(string content)
    {
        await File.WriteAllTextAsync(StockFile, content, Ct);
        var repository = OpenStock();

        await Assert.ThrowsAsync<DataFileException>(async () =>
            _ = await repository.ListAsync(Ct));
        await Assert.ThrowsAsync<DataFileException>(() =>
            repository.AddAsync(TestData.CreateStock(TestData.SecondSku, 1), Ct));

        Assert.Equal(content, await File.ReadAllTextAsync(StockFile, Ct));
    }

    [Fact]
    public async Task Duplicate_key_is_reported_and_never_overwritten()
    {
        const string Row = """{ "branch": "BR001", "sku": "PKG-0001", "quantity": 1, "version": 1 }""";
        var content = $"[{Row},{Row}]";
        await File.WriteAllTextAsync(StockFile, content, Ct);
        var repository = OpenStock();

        var exception = await Assert.ThrowsAsync<DataFileException>(() =>
            repository.AddAsync(TestData.CreateStock(TestData.SecondSku, 1), Ct));

        Assert.Contains(DataFiles.Stock, exception.Message, StringComparison.Ordinal);
        Assert.Equal(content, await File.ReadAllTextAsync(StockFile, Ct));
    }

    [Fact]
    public async Task Enums_are_stored_as_strings()
    {
        var repository = new JsonRepository<Product, Sku, ProductRecord>(
            _folder.OpenStore(),
            DataFiles.Products,
            new ProductMapper());

        await repository.AddAsync(TestData.CreateProduct(taxCategory: TaxCategory.Reduced), Ct);

        var json = await File.ReadAllTextAsync(_folder.PathOf(DataFiles.Products), Ct);
        Assert.Contains("\"taxCategory\": \"Reduced\"", json, StringComparison.Ordinal);
        Assert.Contains("\"category\": \"Packaging\"", json, StringComparison.Ordinal);
    }

    private JsonRepository<StockItem, StockKey, StockItemRecord> OpenStock(JsonFileStore? store = null) =>
        new(store ?? _folder.OpenStore(), DataFiles.Stock, new StockItemMapper());

    private static async Task<StockItem> AddAndLoadAsync(
        JsonRepository<StockItem, StockKey, StockItemRecord> repository,
        int quantity)
    {
        await repository.AddAsync(TestData.CreateStock(Sku, quantity), Ct);
        return (await repository.GetAsync(Key(Sku), Ct))!;
    }

    private static async Task<bool> WonAsync(Task write)
    {
        try
        {
            await write;
            return true;
        }
        catch (ConcurrencyException)
        {
            return false;
        }
    }

    private static StockKey Key(string sku) => TestData.CreateStock(sku, 0).Id;

    private static string SkuNumber(int number) => $"PKG-{number:0000}";
}
