using ApexPost.Application.Validation;

namespace ApexPost.Application.DTOs;

public sealed record AddRetailItemCommand
{
    [NotBlank]
    [Pattern(InputPatterns.Sku, InputPatterns.SkuExample)]
    public string Sku { get; init; } = string.Empty;

    [PositiveNumber]
    public int Quantity { get; init; } = 1;
}
