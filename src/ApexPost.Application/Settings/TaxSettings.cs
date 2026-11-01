using ApexPost.Domain.Rules;

namespace ApexPost.Application.Settings;

/// <summary>The "Tax" section: the percentage per tax category.</summary>
public sealed class TaxSettings
{
    public const string SectionName = "Tax";

    public decimal StandardPercent { get; set; } = 20m;

    public decimal ReducedPercent { get; set; } = 5m;

    public decimal ExemptPercent { get; set; }

    public TaxRates ToRates() => new(StandardPercent, ReducedPercent, ExemptPercent);
}
