using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Domain.Inventory;

namespace ApexPost.Application.Services;

/// <summary>Product search with the stock of one branch: the user's own, or any branch for auditors.</summary>
public sealed class CatalogueService(
    ICurrentUser user,
    IProductCatalog products,
    IRepository<StockItem, StockKey> stock,
    CheckoutRules rules)
{
    public async Task<IReadOnlyList<CatalogueItem>> SearchAsync(
        CatalogueQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var branch = user.ScopeTo(query.Branch) ?? user.Branch;
        var all = await products.ListAsync(cancellationToken).ConfigureAwait(false);
        var levels = (await stock.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(item => item.Branch == branch)
            .ToDictionary(item => item.Sku);
        var search = query.Search?.Trim() ?? string.Empty;

        return
        [
            .. all
                .Where(product => query.Category is null || product.Category == query.Category)
                .Where(product => search.Length == 0
                    || product.Sku.Value.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || product.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(product => product.Sku.Value, StringComparer.Ordinal)
                .Select(product =>
                {
                    var level = levels.GetValueOrDefault(product.Sku) ?? new StockItem(branch, product.Sku, 0);
                    return new CatalogueItem(
                        product.Sku,
                        product.Name,
                        product.Category,
                        product.UnitPrice,
                        product.TaxCategory,
                        level.Quantity,
                        rules.IsLowStock(level));
                }),
        ];
    }
}
