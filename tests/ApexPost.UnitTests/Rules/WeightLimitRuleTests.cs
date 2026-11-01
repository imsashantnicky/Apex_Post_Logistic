using ApexPost.Domain.Common;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Shipping;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 7: letters up to 2 kg; parcels up to 30 / 20 / 10 kg for Standard / Express / Registered.</summary>
public sealed class WeightLimitRuleTests
{
    [Theory]
    [InlineData(ShipmentType.Letter, ServiceLevel.Standard, 2)]
    [InlineData(ShipmentType.Letter, ServiceLevel.Express, 2)]
    [InlineData(ShipmentType.Letter, ServiceLevel.Registered, 2)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Standard, 30)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Express, 20)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Registered, 10)]
    public void Shipment_exactly_at_the_limit_is_accepted(ShipmentType type, ServiceLevel level, int limitKg)
    {
        var shipment = CreateShipment(limitKg, type, level);

        Assert.Equal(Weight.FromKilograms(limitKg), WeightLimitRule.MaximumFor(type, level));
        Assert.Equal(Weight.FromKilograms(limitKg), shipment.Weight);
    }

    [Theory]
    [InlineData(ShipmentType.Letter, ServiceLevel.Standard)]
    [InlineData(ShipmentType.Letter, ServiceLevel.Express)]
    [InlineData(ShipmentType.Letter, ServiceLevel.Registered)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Standard)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Express)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Registered)]
    public void Shipment_one_gram_over_the_limit_is_rejected(ShipmentType type, ServiceLevel level)
    {
        var overLimit = new Weight(WeightLimitRule.MaximumFor(type, level).Grams + 1);

        Assert.False(WeightLimitRule.Allows(type, level, overLimit));
        Assert.Throws<DomainException>(() =>
            new Shipment(type, level, overLimit, new Dimensions(10m, 10m, 10m), CreateAddress()));
    }

    [Fact]
    public void Limit_applies_to_the_actual_weight_not_the_volumetric_weight()
    {
        var bulky = CreateShipment(5m, ShipmentType.Parcel, ServiceLevel.Registered, new Dimensions(100m, 100m, 100m));

        Assert.Equal(200m, bulky.ChargeableKg);
    }

    [Fact]
    public void Undefined_service_level_is_rejected()
    {
        Assert.Throws<DomainException>(() => CreateShipment(1m, ShipmentType.Parcel, (ServiceLevel)99));
        Assert.Throws<DomainException>(() => WeightLimitRule.MaximumFor(ShipmentType.Parcel, (ServiceLevel)99));
    }
}
