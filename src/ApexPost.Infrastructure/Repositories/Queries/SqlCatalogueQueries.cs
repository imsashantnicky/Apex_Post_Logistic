using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories.Queries;

/// <summary>
/// One query for the whole list: the branch quantity is a correlated subquery, not one lookup per product. The cast
/// reads a converted column as its stored value, which is what a LIKE needs. IsLow is left for the service, because
/// the low-stock rule is a domain predicate.
/// </summary>
public sealed class SqlCatalogueQueries(IDbContextFactory<ApexPostDbContext> contexts) : ICatalogueQueries
{
    public async Task<IReadOnlyList<CatalogueItem>> SearchAsync(
        BranchCode branch,
        string search,
        ProductCategory? category,
        CancellationToken cancellationToken)
    {
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            var products = database.Set<Product>().AsNoTracking();
            if (category is { } selected)
            {
                products = products.Where(product => product.Category == selected);
            }

            if (search.Length > 0)
            {
                products = products.Where(product =>
                    product.Name.Contains(search) || ((string)(object)product.Id).Contains(search));
            }

            return await products
                .OrderBy(product => product.Id)
                .Select(product => new CatalogueItem(
                    product.Id,
                    product.Name,
                    product.Category,
                    product.UnitPrice,
                    product.TaxCategory,
                    database.Set<StockItem>()
                        .Where(item => item.Branch == branch && item.Sku == product.Id)
                        .Select(item => (int?)item.Quantity)
                        .FirstOrDefault() ?? 0,
                    false))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
