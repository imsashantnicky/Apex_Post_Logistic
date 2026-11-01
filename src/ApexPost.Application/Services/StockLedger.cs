using ApexPost.Application.Abstractions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;

namespace ApexPost.Application.Services;

/// <summary>Loads and saves the stock items touched by a sale, a reversal or an adjustment.</summary>
public sealed class StockLedger(IRepository<StockItem, StockKey> stock)
{
    /// <summary>The existing stock items of the given products; products without stock are left out.</summary>
    public async Task<List<StockItem>> LoadAsync(
        BranchCode branch,
        IEnumerable<Sku> skus,
        CancellationToken cancellationToken)
    {
        List<StockItem> items = [];
        foreach (var sku in skus.Distinct())
        {
            if (await stock.GetAsync(new StockKey(branch, sku), cancellationToken).ConfigureAwait(false) is { } item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    public Task AddAsync(StockItem item, CancellationToken cancellationToken) =>
        stock.AddAsync(item, cancellationToken);

    public async Task SaveAsync(IEnumerable<StockItem> items, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            await stock.UpdateAsync(item, item.Version, cancellationToken).ConfigureAwait(false);
        }
    }
}
