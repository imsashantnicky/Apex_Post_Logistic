namespace ApexPost.Web.ViewModels;

/// <summary>A report's content with its filter bar.</summary>
public sealed record ReportPage<TReport>(TReport Report, ReportToolbar Toolbar);
