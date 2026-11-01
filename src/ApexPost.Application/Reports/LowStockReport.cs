namespace ApexPost.Application.Reports;

/// <summary>Items at or below the configured threshold.</summary>
public sealed record LowStockReport(int Threshold, IReadOnlyList<LowStockRow> Rows);
