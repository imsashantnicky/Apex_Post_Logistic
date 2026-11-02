namespace ApexPost.Web.ViewModels;

/// <summary>One sidebar link; the layout adds it only when the role may open the page.</summary>
public sealed record NavLink(string Controller, string Action, string Label, string Icon, bool IsCurrent);
