using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Transactions;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 9: Standard 20 %, Reduced 5 %, Exempt 0 %; previewed while open and frozen at payment.</summary>
public sealed class TaxRuleTests
{
    private static readonly Func<ILineItem, Money> Tax = TaxRule.From(StandardTaxRates);

    [Theory]
    [InlineData(TaxCategory.Standard, 2.00)]
    [InlineData(TaxCategory.Reduced, 0.50)]
    [InlineData(TaxCategory.Exempt, 0.00)]
    public void Tax_follows_the_tax_category(TaxCategory category, double expected)
    {
        var line = RetailLineItem.For(CreateProduct(price: 10m, taxCategory: category), 1);

        Assert.Equal(new Money((decimal)expected), Tax(line));
    }

    [Fact]
    public void Money_orders_are_exempt_and_shipping_is_standard_rated()
    {
        var order = MoneyOrderLineItem.Issue(new Money(100m), "Ana Ruiz", "Ben Ruiz");
        var shipping = ShippingLineItem.Create(CreateShipment(2m), CreateZone(), Multipliers);

        Assert.Equal(new Money(0m), Tax(order));
        Assert.Equal(new Money(1.60m), Tax(shipping));
    }

    [Fact]
    public void Line_tax_is_rounded_to_cents()
    {
        Assert.Equal(new Money(0.20m), Tax(RetailLineItem.For(CreateProduct(price: 0.99m), 1)));
    }

    [Fact]
    public void Tax_is_previewed_while_open_and_frozen_at_payment()
    {
        var (transaction, clerk) = OpenBasket();
        transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(FirstSku, 10m, TaxCategory.Standard), 1));
        transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(SecondSku, 10m, TaxCategory.Reduced), 1));

        Assert.Equal(new Money(2.50m), transaction.PreviewTax(Tax));
        Assert.Equal(new Money(0m), transaction.Tax);

        transaction.Pay(
            clerk, PaymentMethod.Card, CreateRules(), [CreateStock(FirstSku, 5), CreateStock(SecondSku, 5)], Now);

        var higherRates = TaxRule.From(new TaxRates(50m, 50m, 50m));
        Assert.Equal(new Money(2.50m), transaction.Tax);
        Assert.Equal(new Money(2.50m), transaction.PreviewTax(higherRates));
        Assert.Equal(new Money(22.50m), transaction.Total);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 100, 100)]
    public void Rates_from_0_to_100_percent_are_accepted(double standard, double reduced, double exempt)
    {
        var rates = new TaxRates((decimal)standard, (decimal)reduced, (decimal)exempt);

        Assert.Equal((decimal)standard, rates.PercentFor(TaxCategory.Standard));
    }

    [Theory]
    [InlineData(-1, 5, 0)]
    [InlineData(20, 100.01, 0)]
    [InlineData(20, 5, -0.01)]
    public void Rates_outside_0_to_100_percent_are_rejected(double standard, double reduced, double exempt)
    {
        Assert.Throws<DomainException>(() => new TaxRates((decimal)standard, (decimal)reduced, (decimal)exempt));
    }
}
