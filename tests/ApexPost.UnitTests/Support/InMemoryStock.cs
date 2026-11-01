using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;

namespace ApexPost.UnitTests.Support;

/// <summary>The stock port on top of the in-memory store; adjustments are recorded for the tests to read.</summary>
internal sealed class InMemoryStock(InMemoryRepository<StockItem, StockKey> store, FakeUnitOfWork unitOfWork)
    : IStockRepository
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

    public void Add(StockItem item) => unitOfWork.Stage(token => store.AddAsync(item, token));

    public void Update(IEnumerable<StockItem> items)
    {
        foreach (var item in items)
        {
            unitOfWork.Stage(token => store.UpdateAsync(item, item.Version, token));
        }
    }

    public void AddAdjustment(StockAdjustment adjustment, Guid staffId, DateTimeOffset at) =>
        unitOfWork.Stage(_ =>
        {
            Adjustments.Add(adjustment);
            return Task.CompletedTask;
        });
}
