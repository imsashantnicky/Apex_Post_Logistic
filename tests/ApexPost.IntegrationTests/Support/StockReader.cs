using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Support;

internal static class StockReader
{
    public static async Task<int> QuantityAsync(this ApexFactory factory, string sku, string branch = "BR001")
    {
        var branchCode = new BranchCode(branch);
        var productSku = new Sku(sku);
        await using var database = factory.OpenDatabase();
        return await database.Set<StockItem>()
            .AsNoTracking()
            .Where(item => item.Branch == branchCode && item.Sku == productSku)
            .Select(item => item.Quantity)
            .SingleOrDefaultAsync();
    }

    /// <summary>Sets a quantity directly, as another server or a stock take would.</summary>
    public static async Task SetQuantityAsync(
        this ApexFactory factory,
        string sku,
        int quantity,
        string branch = "BR001")
    {
        var branchCode = new BranchCode(branch);
        var productSku = new Sku(sku);
        await using var database = factory.OpenDatabase();
        await database.Set<StockItem>()
            .Where(item => item.Branch == branchCode && item.Sku == productSku)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Quantity, quantity));
    }
}
