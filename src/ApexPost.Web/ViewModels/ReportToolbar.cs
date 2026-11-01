using System.Globalization;

namespace ApexPost.Web.ViewModels;

/// <summary>The filter of a report page: the branch picker, what it selects and, where they apply, the dates.</summary>
public sealed record ReportToolbar(BranchFilter Branches, string Scope, DateOnly? From = null, DateOnly? To = null)
{
    public const string DateFormat = "yyyy-MM-dd";

    public string? FromText => From?.ToString(DateFormat, CultureInfo.InvariantCulture);

    public string? ToText => To?.ToString(DateFormat, CultureInfo.InvariantCulture);

    public string Subtitle => From is null ? Scope : $"{Scope} · {FromText} to {ToText}";
}
