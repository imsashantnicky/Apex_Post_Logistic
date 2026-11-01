using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;

namespace ApexPost.UnitTests.Support;

internal sealed class InMemoryCatalogueQueries(
    IProductCatalog products,
    InMemoryRepository<StockItem, StockKey> stock) : ICatalogueQueries
{
    public async Task<IReadOnlyList<CatalogueItem>> SearchAsync(
        BranchCode branch,
        string search,
        ProductCategory? category,
        CancellationToken cancellationToken)
    {
        var all = await products.ListAsync(cancellationToken);
        var levels = (await stock.ListAsync(cancellationToken))
            .Where(item => item.Branch == branch)
            .ToDictionary(item => item.Sku);
        return
        [
            .. all
                .Where(product => category is null || product.Category == category)
                .Where(product => search.Length == 0
                    || product.Sku.Value.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || product.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(product => product.Sku.Value, StringComparer.Ordinal)
                .Select(product => new CatalogueItem(
                    product.Sku,
                    product.Name,
                    product.Category,
                    product.UnitPrice,
                    product.TaxCategory,
                    levels.GetValueOrDefault(product.Sku)?.Quantity ?? 0,
                    false)),
        ];
    }
}
