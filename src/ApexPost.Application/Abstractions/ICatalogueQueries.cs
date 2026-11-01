using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;

namespace ApexPost.Application.Abstractions;

/// <summary>The product search, ordered by SKU; a product the branch does not hold shows 0 in stock.</summary>
public interface ICatalogueQueries
{
    Task<IReadOnlyList<CatalogueItem>> SearchAsync(
        BranchCode branch,
        string search,
        ProductCategory? category,
        CancellationToken cancellationToken);
}
