namespace ApexPost.Web.Filters;

/// <summary>The "Performance" section: actions slower than this are logged as a warning.</summary>
public sealed class PerformanceSettings
{
    public const string SectionName = "Performance";

    public int SlowActionMs { get; set; } = 500;
}
