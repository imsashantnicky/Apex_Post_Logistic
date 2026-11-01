using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Application.DTOs;

/// <summary>A product with the stock held by the selected branch.</summary>
public sealed record CatalogueItem(
    Sku Sku,
    string Name,
    ProductCategory Category,
    Money UnitPrice,
    TaxCategory TaxCategory,
    int InStock,
    bool IsLow);
