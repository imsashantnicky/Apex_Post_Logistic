using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;

namespace ApexPost.Application.DTOs;

/// <summary>The result of a manual stock adjustment, kept for the audit trail.</summary>
public sealed record StockAdjustment(BranchCode Branch, Sku Sku, int Change, int NewQuantity, string Reason);
