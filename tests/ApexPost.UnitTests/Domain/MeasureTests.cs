using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Domain;

public sealed class MeasureTests
{
    [Fact]
    public void Weight_converts_kilograms_to_grams()
    {
        var weight = Weight.FromKilograms(1.5m);

        Assert.Equal(1500, weight.Grams);
        Assert.Equal(1.5m, weight.Kilograms);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Weight_must_be_positive(int grams)
    {
        Assert.Throws<DomainException>(() => new Weight(grams));
    }

    [Fact]
    public void One_gram_is_the_lightest_weight()
    {
        Assert.Equal(1, Weight.FromKilograms(0.001m).Grams);
        Assert.Throws<DomainException>(() => Weight.FromKilograms(0.0004m));
    }

    [Fact]
    public void Weights_compare_by_grams()
    {
        Assert.True(new Weight(999) < Weight.FromKilograms(1m));
        Assert.True(new Weight(1000) <= Weight.FromKilograms(1m));
        Assert.True(new Weight(1001) > Weight.FromKilograms(1m));
    }

    [Fact]
    public void Volumetric_weight_is_volume_divided_by_5000()
    {
        Assert.Equal(12m, new Dimensions(50m, 40m, 30m).VolumetricKg);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, -1)]
    public void Every_dimension_must_be_positive(int length, int width, int height)
    {
        Assert.Throws<DomainException>(() => new Dimensions(length, width, height));
    }

    [Fact]
    public void Address_parts_are_trimmed()
    {
        var address = CreateAddress() with { City = "  York  " };

        Assert.Equal("York", address.City);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Address_parts_are_required(string blank)
    {
        Assert.Throws<DomainException>(() => CreateAddress() with { Street = blank });
        Assert.Throws<DomainException>(() => CreateAddress() with { PostalCode = blank });
    }

    [Fact]
    public void Address_street_has_a_maximum_length()
    {
        var atLimit = new string('a', TextLimits.AddressLine);

        Assert.Equal(atLimit, (CreateAddress() with { Street = atLimit }).Street);
        Assert.Throws<DomainException>(() => CreateAddress() with { Street = atLimit + "a" });
    }

    [Fact]
    public void Chargeable_weight_is_the_larger_of_actual_and_volumetric()
    {
        Assert.Equal(12m, CreateShipment(2m, dimensions: new Dimensions(50m, 40m, 30m)).ChargeableKg);
        Assert.Equal(5m, CreateShipment(5m, dimensions: new Dimensions(10m, 10m, 10m)).ChargeableKg);
    }
}
