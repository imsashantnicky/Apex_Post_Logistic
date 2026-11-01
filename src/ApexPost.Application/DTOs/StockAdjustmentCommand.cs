using ApexPost.Application.Validation;
using ApexPost.Domain.Common;

namespace ApexPost.Application.DTOs;

/// <summary>A manual stock correction: a positive change adds stock, a negative one removes it.</summary>
public sealed record StockAdjustmentCommand
{
    [NotBlank]
    [Pattern(InputPatterns.Sku, InputPatterns.SkuExample)]
    public string Sku { get; init; } = string.Empty;

    public int Change { get; init; }

    [NotBlank]
    [TextLength(TextLimits.Reason)]
    public string Reason { get; init; } = string.Empty;
}
