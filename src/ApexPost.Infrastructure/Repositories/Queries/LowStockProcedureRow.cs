namespace ApexPost.Infrastructure.Repositories.Queries;

/// <summary>One row of dbo.usp_LowStockReport; the property names are its column names.</summary>
public sealed class LowStockProcedureRow
{
    public string BranchCode { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string Product { get; init; } = string.Empty;

    public int Quantity { get; init; }
}
