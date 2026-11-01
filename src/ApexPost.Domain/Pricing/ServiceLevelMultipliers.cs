using ApexPost.Domain.Common;

namespace ApexPost.Domain.Pricing;

/// <summary>Price multiplier per service level, bound from configuration.</summary>
public sealed record ServiceLevelMultipliers(decimal Standard, decimal Express, decimal Registered)
{
    public decimal Standard { get; } = Guard.Positive(Standard, "Standard multiplier");

    public decimal Express { get; } = Guard.Positive(Express, "Express multiplier");

    public decimal Registered { get; } = Guard.Positive(Registered, "Registered multiplier");
}
