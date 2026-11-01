using ApexPost.Domain.Common;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>
/// Rule 8: price = (zone base rate + zone rate per kg * chargeable kg) * level multiplier,
/// chargeable kg = max(actual, volumetric). Test zone: base 5.00, per kg 1.50.
/// </summary>
public sealed class ShippingPricingRuleTests
{
    [Theory]
    [InlineData(ServiceLevel.Standard, 8.00)]
    [InlineData(ServiceLevel.Express, 12.80)]
    [InlineData(ServiceLevel.Registered, 10.40)]
    public void Service_level_multiplier_is_applied(ServiceLevel level, double expected)
    {
        var price = ShippingPricing.Quote(CreateShipment(2m, level: level), CreateZone(), Multipliers);

        Assert.Equal(new Money((decimal)expected), price);
    }

    [Theory]
    [InlineData(1, 6.50)]
    [InlineData(10, 20.00)]
    [InlineData(30, 50.00)]
    public void Price_grows_with_the_weight_band(int kilograms, double expected)
    {
        var price = ShippingPricing.Quote(CreateShipment(kilograms), CreateZone(), Multipliers);

        Assert.Equal(new Money((decimal)expected), price);
    }

    [Fact]
    public void Volumetric_weight_is_charged_when_it_is_larger()
    {
        var bulky = CreateShipment(2m, dimensions: new Dimensions(50m, 40m, 30m));

        Assert.Equal(new Money(23.00m), ShippingPricing.Quote(bulky, CreateZone(), Multipliers));
    }

    [Fact]
    public void Zone_rates_change_the_price()
    {
        var farZone = new ShippingZone("Z10", "Overseas", new Money(9m), new Money(3m));

        Assert.Equal(new Money(15.00m), ShippingPricing.Quote(CreateShipment(2m), farZone, Multipliers));
    }

    [Fact]
    public void Price_is_rounded_to_cents()
    {
        var shipment = CreateShipment(1.333m, level: ServiceLevel.Express);

        Assert.Equal(new Money(11.20m), ShippingPricing.Quote(shipment, CreateZone(), Multipliers));
    }

    [Theory]
    [InlineData(ServiceLevel.Standard, typeof(StandardShippingPricing))]
    [InlineData(ServiceLevel.Express, typeof(ExpressShippingPricing))]
    [InlineData(ServiceLevel.Registered, typeof(RegisteredShippingPricing))]
    public void Strategy_is_chosen_by_service_level(ServiceLevel level, Type expected)
    {
        Assert.IsType(expected, ShippingPricing.For(level, Multipliers));
    }

    [Fact]
    public void Shipping_line_uses_the_quoted_price()
    {
        var shipment = CreateShipment(2m, level: ServiceLevel.Registered);

        var line = ShippingLineItem.Create(shipment, CreateZone(), Multipliers);

        Assert.Equal(new Money(10.40m), line.Total);
    }

    [Theory]
    [InlineData(0, 1.6, 1.3)]
    [InlineData(1, -1, 1.3)]
    [InlineData(1, 1.6, 0)]
    public void Multipliers_must_be_positive(double standard, double express, double registered)
    {
        Assert.Throws<DomainException>(() =>
            new ServiceLevelMultipliers((decimal)standard, (decimal)express, (decimal)registered));
    }
}
