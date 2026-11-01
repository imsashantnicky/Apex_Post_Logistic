using ApexPost.Application.Validation;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Application.DTOs;

/// <summary>A percentage price change for one category (null = all products), for example 5 or -2.5.</summary>
public sealed record PriceRevisionCommand
{
    /// <summary>The largest change at once; a cut of at most 50 % can never take a price down to zero.</summary>
    public const decimal MaximumPercent = 50m;

    public ProductCategory? Category { get; init; }

    public decimal Percent { get; init; }

    [NotBlank]
    [TextLength(TextLimits.Reason)]
    public string Reason { get; init; } = string.Empty;
}
