using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;

namespace ApexPost.Web.ViewModels;

/// <summary>The branch picker for staff who work across branches; empty choices hide it.</summary>
public sealed record BranchFilter(IReadOnlyList<BranchSummary> Choices, BranchCode? Selected, bool AllowAll);
