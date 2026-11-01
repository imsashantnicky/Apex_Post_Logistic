using ApexPost.Application.Abstractions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Support;

internal static class StockReader
{
    public static async Task<int> QuantityAsync(this ApexFactory factory, string sku, string branch = "BR001")
    {
        var stock = factory.Services.GetRequiredService<IRepository<StockItem, StockKey>>();
        var item = await stock.GetAsync(new StockKey(new BranchCode(branch), new Sku(sku)), CancellationToken.None);
        return item?.Quantity ?? 0;
    }
}
