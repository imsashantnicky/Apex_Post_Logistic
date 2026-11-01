using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;

namespace ApexPost.Application.DTOs;

/// <summary>Search text (SKU or name), optional category and branch; null branch means the user's own.</summary>
public sealed record CatalogueQuery(string? Search = null, ProductCategory? Category = null, BranchCode? Branch = null);
