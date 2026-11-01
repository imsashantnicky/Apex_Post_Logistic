using ApexPost.Application.Validation;
using ApexPost.Domain.Common;

namespace ApexPost.Application.DTOs;

public sealed record AddMoneyOrderCommand
{
    [PositiveNumber]
    public decimal Amount { get; init; }

    [NotBlank]
    [TextLength(TextLimits.Name)]
    public string Sender { get; init; } = string.Empty;

    [NotBlank]
    [TextLength(TextLimits.Name)]
    public string Recipient { get; init; } = string.Empty;
}
