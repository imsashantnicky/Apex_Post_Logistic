using ApexPost.Domain.Catalogue;

namespace ApexPost.Infrastructure.Persistence.Records;

/// <summary>A row of the ProductCategories reference table; one row per <see cref="ProductCategory"/> value.</summary>
public sealed class ProductCategoryRecord
{
    public ProductCategory Name { get; set; }
}
