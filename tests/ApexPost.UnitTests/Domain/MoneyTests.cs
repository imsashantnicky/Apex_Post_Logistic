using ApexPost.Domain.Common;

namespace ApexPost.UnitTests.Domain;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(1.005, 1.01)]
    [InlineData(1.004, 1.00)]
    [InlineData(2.499, 2.50)]
    public void Amount_is_rounded_to_two_decimals_away_from_zero(double amount, double expected)
    {
        Assert.Equal((decimal)expected, new Money((decimal)amount).Amount);
    }

    [Fact]
    public void Zero_is_the_smallest_valid_amount()
    {
        Assert.Equal(0m, new Money(0m).Amount);
        Assert.Throws<DomainException>(() => new Money(-0.01m));
    }

    [Fact]
    public void Currency_defaults_to_USD_and_is_upper_cased()
    {
        Assert.Equal("USD", new Money(1m).Currency);
        Assert.Equal("EUR", new Money(1m, " eur ").Currency);
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("EURO")]
    [InlineData("U1D")]
    public void Currency_must_be_three_letters(string currency)
    {
        Assert.Throws<DomainException>(() => new Money(1m, currency));
    }

    [Fact]
    public void Arithmetic_keeps_the_currency()
    {
        var price = new Money(2.50m);

        Assert.Equal(new Money(3.75m), price + new Money(1.25m));
        Assert.Equal(new Money(1.25m), price - new Money(1.25m));
        Assert.Equal(new Money(7.50m), price * 3);
    }

    [Fact]
    public void Subtracting_below_zero_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Money(1m) - new Money(1.01m));
    }

    [Fact]
    public void Currencies_cannot_be_mixed()
    {
        var dollars = new Money(1m);
        var euros = new Money(1m, "EUR");

        Assert.Throws<DomainException>(() => dollars + euros);
        Assert.Throws<DomainException>(() => dollars < euros);
    }

    [Fact]
    public void Comparison_uses_the_amount()
    {
        Assert.True(new Money(1m) < new Money(1.01m));
        Assert.True(new Money(1m) >= new Money(1m));
        Assert.False(new Money(2m) <= new Money(1.99m));
    }

    [Fact]
    public void Extension_members_report_sign_and_percent()
    {
        Assert.False(new Money(0m).IsPositive);
        Assert.True(new Money(0.01m).IsPositive);
        Assert.Equal(new Money(2.00m), new Money(10m).Percent(20m));
    }

    [Fact]
    public void Text_form_shows_two_decimals_and_currency()
    {
        Assert.Equal("12.50 USD", new Money(12.5m).ToString());
    }
}
