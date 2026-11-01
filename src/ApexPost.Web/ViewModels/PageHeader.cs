namespace ApexPost.Web.ViewModels;

/// <summary>Page title, optional status pill, muted line (default: branch and date) and one line of help.</summary>
public sealed record PageHeader(string Title, string? Subtitle = null, string? Help = null, string? Status = null);
