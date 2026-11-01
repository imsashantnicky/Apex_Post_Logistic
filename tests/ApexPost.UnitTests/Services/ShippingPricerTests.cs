using ApexPost.Application.Exceptions;
using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Options;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

/// <summary>Zone Z01: base 5.00, 1.50 per kg; multipliers Standard 1.0, Express 1.6, Registered 1.3.</summary>
public sealed class ShippingPricerTests
{
    [Theory]
    [InlineData(ServiceLevel.Standard, 8.00)]
    [InlineData(ServiceLevel.Express, 12.80)]
    [InlineData(ServiceLevel.Registered, 10.40)]
    public async Task Price_uses_zone_rates_weight_and_level(ServiceLevel level, double expected)
    {
        var line = await Pricer().CreateLineAsync(TestData.CreateShipment(2m, level: level), "z01", Ct);

        Assert.Equal(new Money((decimal)expected), line.Total);
        Assert.Equal(ServiceHarness.ZoneCode, line.ZoneCode);
    }

    [Fact]
    public async Task Volumetric_weight_is_charged_when_the_parcel_is_bulky()
    {
        var bulky = TestData.CreateShipment(2m, dimensions: new Dimensions(50m, 40m, 30m));

        var line = await Pricer().CreateLineAsync(bulky, ServiceHarness.ZoneCode, Ct);

        Assert.Equal(12m, bulky.ChargeableKg);
        Assert.Equal(new Money(23.00m), line.Total);
    }

    [Fact]
    public async Task Actual_weight_is_charged_when_it_is_heavier_than_the_volume()
    {
        var heavy = TestData.CreateShipment(20m, dimensions: new Dimensions(50m, 40m, 30m));

        var line = await Pricer().CreateLineAsync(heavy, ServiceHarness.ZoneCode, Ct);

        Assert.Equal(new Money(35.00m), line.Total);
    }

    [Fact]
    public async Task Configured_multiplier_changes_the_price()
    {
        var settings = new ShippingSettings { ExpressMultiplier = 2.0m };

        var line = await Pricer(settings).CreateLineAsync(
            TestData.CreateShipment(2m, level: ServiceLevel.Express),
            ServiceHarness.ZoneCode,
            Ct);

        Assert.Equal(new Money(16.00m), line.Total);
    }

    [Fact]
    public async Task Unknown_zone_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Pricer().CreateLineAsync(TestData.CreateShipment(2m), "Z99", Ct).AsTask());
    }

    private static ShippingPricer Pricer(ShippingSettings? settings = null)
    {
        var catalog = new InMemoryCatalog();
        catalog.Add(TestData.CreateZone());
        return new ShippingPricer(catalog, Options.Create(settings ?? new ShippingSettings()));
    }
}
