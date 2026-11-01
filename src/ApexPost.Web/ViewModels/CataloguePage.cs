using ApexPost.Application.DTOs;
using ApexPost.Domain.Catalogue;

namespace ApexPost.Web.ViewModels;

public sealed record CataloguePage(
    IReadOnlyList<CatalogueItem> Items,
    string? Search,
    ProductCategory? Category,
    BranchFilter Filter,
    bool CanAdjust);
