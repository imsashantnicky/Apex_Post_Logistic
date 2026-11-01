using ApexPost.Application.Validation;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.DTOs;

/// <summary>The editable details of a staff member.</summary>
public sealed record StaffCommand
{
    [NotBlank]
    [TextLength(TextLimits.Name)]
    public string Name { get; init; } = string.Empty;

    [NotBlank]
    [TextLength(TextLimits.Email)]
    [Pattern(InputPatterns.Email, InputPatterns.EmailExample)]
    public string Email { get; init; } = string.Empty;

    public StaffRole Role { get; init; } = StaffRole.CounterClerk;

    [NotBlank]
    [Pattern(InputPatterns.BranchCode, InputPatterns.BranchCodeExample)]
    public string HomeBranch { get; init; } = string.Empty;
}
