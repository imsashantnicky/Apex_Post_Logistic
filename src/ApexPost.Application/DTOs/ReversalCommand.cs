using ApexPost.Application.Validation;
using ApexPost.Domain.Common;

namespace ApexPost.Application.DTOs;

/// <summary>The reason a Branch Manager gives for a void or a refund.</summary>
public sealed record ReversalCommand
{
    [NotBlank]
    [TextLength(TextLimits.Reason)]
    public string Reason { get; init; } = string.Empty;
}
