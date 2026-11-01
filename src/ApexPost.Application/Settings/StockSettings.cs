namespace ApexPost.Application.Settings;

/// <summary>The "Stock" section: a branch item at or below this quantity counts as low stock.</summary>
public sealed class StockSettings
{
    public const string SectionName = "Stock";

    public int LowStockThreshold { get; set; } = 5;
}
