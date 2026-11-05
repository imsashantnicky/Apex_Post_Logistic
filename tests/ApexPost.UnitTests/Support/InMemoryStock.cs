using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;

namespace ApexPost.UnitTests.Support;

/// <summary>
/// The stock ports on top of the in-memory store. Reads and sales go through the unit of work; an adjustment commits
/// at once like the stored procedure, and is recorded for the tests to read.
/// </summary>
internal sealed class InMemoryStock(InMemoryRepository<StockItem, StockKey> store, FakeUnitOfWork unitOfWork)
    : IStockRepository, IStockAdjuster
{
    public List<StockAdjustment> Adjustments { get; } = [];

    public async Task<IReadOnlyList<StockItem>> GetManyAsync(
        BranchCode branch,
        IEnumerable<Sku> skus,
        CancellationToken cancellationToken)
    {
        List<StockItem> items = [];
        foreach (var sku in skus.Distinct())
        {
            if (await store.GetAsync(new StockKey(branch, sku), cancellationToken) is { } item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    public void Update(IEnumerable<StockItem> items)
    {
        foreach (var item in items)
        {
            unitOfWork.Stage(token => store.UpdateAsync(item, item.Version, token));
        }
    }

    public async Task<int> AdjustAsync(
        BranchCode branch,
        Sku sku,
        int change,
        string reason,
        Guid staffId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var existing = await store.GetAsync(new StockKey(branch, sku), cancellationToken);
        var quantity = existing is null
            ? await CreateAsync(branch, sku, change, cancellationToken)
            : await ChangeAsync(existing, change, cancellationToken);
        Adjustments.Add(new StockAdjustment(branch, sku, change, quantity, reason));
        return quantity;
    }

    private async Task<int> CreateAsync(BranchCode branch, Sku sku, int change, CancellationToken cancellationToken)
    {
        if (change < 0)
        {
            throw new InsufficientStockException(sku, -change, 0);
        }

        await store.AddAsync(new StockItem(branch, sku, change), cancellationToken);
        return change;
    }

    private async Task<int> ChangeAsync(StockItem item, int change, CancellationToken cancellationToken)
    {
        if (change > 0)
        {
            item.Add(change);
        }
        else
        {
            item.Remove(-change);
        }

        await store.UpdateAsync(item, item.Version, cancellationToken);
        return item.Quantity;
    }
}
