using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Domain.Inventory;

namespace ApexPost.Application.Services;

/// <summary>Product search with the stock of one branch: the user's own, or any branch for auditors.</summary>
public sealed class CatalogueService(ICurrentUser user, ICatalogueQueries queries, CheckoutRules rules)
{
    public async Task<IReadOnlyList<CatalogueItem>> SearchAsync(
        CatalogueQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var branch = user.ScopeTo(query.Branch) ?? user.Branch;
        var search = query.Search?.Trim() ?? string.Empty;
        var items = await queries.SearchAsync(branch, search, query.Category, cancellationToken).ConfigureAwait(false);
        return
        [
            .. items.Select(item => item with
            {
                IsLow = rules.IsLowStock(new StockItem(branch, item.Sku, item.InStock)),
            }),
        ];
    }
}
