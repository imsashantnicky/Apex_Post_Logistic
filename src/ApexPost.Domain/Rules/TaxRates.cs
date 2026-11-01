using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Rules;

/// <summary>Tax percentages per tax category, bound from configuration.</summary>
public sealed record TaxRates(decimal StandardPercent, decimal ReducedPercent, decimal ExemptPercent)
{
    private const decimal MaximumPercent = 100m;

    public decimal StandardPercent { get; } = ValidPercent(StandardPercent, "standard");

    public decimal ReducedPercent { get; } = ValidPercent(ReducedPercent, "reduced");

    public decimal ExemptPercent { get; } = ValidPercent(ExemptPercent, "exempt");

    public decimal PercentFor(TaxCategory category) => category switch
    {
        TaxCategory.Standard => StandardPercent,
        TaxCategory.Reduced => ReducedPercent,
        TaxCategory.Exempt => ExemptPercent,
        _ => throw new DomainException($"Tax category '{category}' is not valid."),
    };

    private static decimal ValidPercent(decimal percent, string name) =>
        percent is >= 0m and <= MaximumPercent
            ? percent
            : throw new DomainException($"The {name} tax rate must be between 0 and 100 percent.");
}
