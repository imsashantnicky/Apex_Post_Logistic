namespace ApexPost.Web.ViewModels;

public sealed record KpiCard(string Label, string Value, string? Unit = null, bool IsTotal = false);
