using ApexPost.Application.DTOs;

namespace ApexPost.Web.ViewModels;

/// <summary>The add or edit form of a staff member; a null member is a new one, which also needs a password.</summary>
public sealed record StaffFormPage(
    StaffSummary? Member,
    IReadOnlyList<BranchSummary> Branches,
    int MinimumPasswordLength,
    bool IsOwnAccount = false);
