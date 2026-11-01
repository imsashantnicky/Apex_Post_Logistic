using ApexPost.Domain.Pricing;

namespace ApexPost.Application.Settings;

/// <summary>The "Shipping" section: the price multiplier per service level.</summary>
public sealed class ShippingSettings
{
    public const string SectionName = "Shipping";

    public decimal StandardMultiplier { get; set; } = 1.0m;

    public decimal ExpressMultiplier { get; set; } = 1.6m;

    public decimal RegisteredMultiplier { get; set; } = 1.3m;

    public ServiceLevelMultipliers ToMultipliers() => new(StandardMultiplier, ExpressMultiplier, RegisteredMultiplier);
}
