using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;

namespace ApexPost.Application.Reports;

public sealed record LowStockRow(BranchCode Branch, Sku Sku, string Product, int Quantity);
