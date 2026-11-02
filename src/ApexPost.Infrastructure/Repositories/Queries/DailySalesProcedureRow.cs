namespace ApexPost.Infrastructure.Repositories.Queries;

/// <summary>
/// One row of dbo.usp_DailyBranchSalesRange, and of usp_DailyBranchSales over it; the property names are its columns.
/// </summary>
public sealed class DailySalesProcedureRow
{
    public DateOnly SalesDay { get; init; }

    public string BranchCode { get; init; } = string.Empty;

    public int Transactions { get; init; }

    public decimal Retail { get; init; }

    public decimal Services { get; init; }

    public decimal Tax { get; init; }
}
