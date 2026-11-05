using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

/// <summary>A destination zone with a base rate and a rate per chargeable kilogram.</summary>
public sealed class ShippingZone : Entity<string>
{
    public ShippingZone(string code, string name, Money baseRate, Money perKgRate)
        : base(Guard.NotBlank(code, "Zone code", TextLimits.ZoneCode).ToUpperInvariant())
    {
        Name = Guard.NotBlank(name, nameof(Name), TextLimits.Name);
        BaseRate = PositiveRate(baseRate, "base rate");
        PerKgRate = PositiveRate(perKgRate, "rate per kg");
        if (BaseRate.Currency != PerKgRate.Currency)
        {
            throw new DomainException("Both zone rates must use the same currency.");
        }
    }

    // Only for persistence frameworks; they fill the properties from stored values.
    private ShippingZone()
    {
        Name = null!;
        BaseRate = null!;
        PerKgRate = null!;
    }

    public string Code => Id;

    public string Name { get; }

    public Money BaseRate { get; }

    public Money PerKgRate { get; }

    private static Money PositiveRate(Money rate, string name) =>
        Guard.Required(rate, name).IsPositive
            ? rate
            : throw new DomainException($"The zone {name} must be greater than zero.");
}
