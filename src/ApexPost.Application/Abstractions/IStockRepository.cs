using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;

namespace ApexPost.Application.Abstractions;

/// <summary>Branch stock for writing; nothing is stored until the unit of work commits.</summary>
public interface IStockRepository
{
    /// <summary>The existing stock items of the given products; products without stock are left out.</summary>
    Task<IReadOnlyList<StockItem>> GetManyAsync(
        BranchCode branch,
        IEnumerable<Sku> skus,
        CancellationToken cancellationToken);

    /// <summary>Stages changes that fail with a conflict if someone else changed an item since it was read.</summary>
    void Update(IEnumerable<StockItem> items);
}
