using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;

namespace ApexPost.Domain.Inventory;

/// <summary>Moves the stock of a basket's retail lines. Every line is checked before any quantity changes.</summary>
internal static class StockAllocation
{
    /// <summary>Removes the basket quantities and returns the stock items that changed.</summary>
    public static List<StockItem> Take(
        BranchCode branch,
        IEnumerable<RetailLineItem> lines,
        IReadOnlyCollection<StockItem> stock)
    {
        var demand = Demand(branch, lines, stock);
        foreach (var (key, quantity, item) in demand)
        {
            if (item is null || !item.CanSupply(quantity))
            {
                throw new InsufficientStockException(key.Sku, quantity, item?.Quantity ?? 0);
            }
        }

        foreach (var (_, quantity, item) in demand)
        {
            item?.Remove(quantity);
        }

        return [.. demand.Select(entry => entry.Item).OfType<StockItem>()];
    }

    public static void Restock(
        BranchCode branch,
        IEnumerable<RetailLineItem> lines,
        IReadOnlyCollection<StockItem> stock)
    {
        var demand = Demand(branch, lines, stock);
        var missing = demand.Find(entry => entry.Item is null);
        if (missing.Key is not null)
        {
            throw new DomainException($"There is no stock record for {missing.Key}.");
        }

        foreach (var (_, quantity, item) in demand)
        {
            item?.Add(quantity);
        }
    }

    private static List<(StockKey Key, int Quantity, StockItem? Item)> Demand(
        BranchCode branch,
        IEnumerable<RetailLineItem> lines,
        IReadOnlyCollection<StockItem> stock) =>
    [
        .. lines
            .GroupBy(line => new StockKey(branch, line.Sku))
            .Select(group => (
                group.Key,
                group.Sum(line => line.Quantity),
                stock.FirstOrDefault(item => item.Id == group.Key))),
    ];
}
