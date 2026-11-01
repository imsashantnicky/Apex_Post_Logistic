using ApexPost.Application.Abstractions;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories;

public sealed class ProductRepository(ApexPostDbContext database) : IProductRepository
{
    private const decimal WholePrice = 100m;
    private const int PriceDecimals = 2;

    /// <summary>
    /// One UPDATE instead of one per product. It bypasses the change tracker and so the audit-columns interceptor,
    /// which is why ModifiedAtUtc is set here. Percent stays apart from a factor because EF sends this parameter with
    /// the scale of a Money column, and a factor such as 1.125 would be rounded.
    /// </summary>
    public Task<int> RevisePricesAsync(
        ProductCategory? category,
        decimal percent,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var products = database.Set<Product>().AsQueryable();
        if (category is { } selected)
        {
            products = products.Where(product => product.Category == selected);
        }

        var share = WholePrice + percent;
        var modifiedAt = at.UtcDateTime;
        return products.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(
                    product => product.UnitPrice,
                    product => (Money)(object)Math.Round(
                        (decimal)(object)product.UnitPrice * share / WholePrice,
                        PriceDecimals))
                .SetProperty(product => EF.Property<DateTime?>(product, ColumnRules.ModifiedAt), modifiedAt),
            cancellationToken);
    }
}
