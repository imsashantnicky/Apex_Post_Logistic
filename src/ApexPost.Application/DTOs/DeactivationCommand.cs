using ApexPost.Application.Validation;
using ApexPost.Domain.Common;

namespace ApexPost.Application.DTOs;

/// <summary>Why an administrator deactivates a staff member.</summary>
public sealed record DeactivationCommand
{
    [NotBlank]
    [TextLength(TextLimits.Reason)]
    public string Reason { get; init; } = string.Empty;
}
