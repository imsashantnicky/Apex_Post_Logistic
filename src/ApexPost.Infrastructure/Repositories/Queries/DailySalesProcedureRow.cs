namespace ApexPost.Infrastructure.Repositories.Queries;

/// <summary>One row of dbo.usp_DailyBranchSales; the property names are its column names.</summary>
public sealed class DailySalesProcedureRow
{
    public DateOnly SalesDay { get; init; }

    public string BranchCode { get; init; } = string.Empty;

    public int Transactions { get; init; }

    public decimal Retail { get; init; }

    public decimal Services { get; init; }

    public decimal Tax { get; init; }
}
