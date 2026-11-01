using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Domain;

public sealed class LineItemTests
{
    [Fact]
    public void Retail_line_copies_the_product_at_the_time_of_sale()
    {
        var product = CreateProduct(price: 2.25m, taxCategory: TaxCategory.Reduced);

        var line = RetailLineItem.For(product, 4);

        Assert.Equal(product.Sku, line.Sku);
        Assert.Equal(product.Name, line.Description);
        Assert.Equal(TaxCategory.Reduced, line.TaxCategory);
        Assert.Equal(new Money(9.00m), line.Total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Retail_quantity_must_be_positive(int quantity)
    {
        Assert.Throws<DomainException>(() => RetailLineItem.For(CreateProduct(), quantity));
    }

    [Fact]
    public void Shipping_line_has_a_tracking_number_and_standard_tax()
    {
        var line = ShippingLineItem.Create(CreateShipment(1m), CreateZone(), Multipliers);

        Assert.Matches("^AP[0-9A-F]{10}$", line.TrackingNumber);
        Assert.Equal(TaxCategory.Standard, line.TaxCategory);
        Assert.Equal("Z01", line.ZoneCode);
    }

    [Fact]
    public void Shipping_price_must_be_positive()
    {
        Assert.Throws<DomainException>(() =>
            new ShippingLineItem(Guid.NewGuid(), CreateShipment(1m), "Z01", new Money(0m)));
    }

    [Fact]
    public void Money_order_total_is_amount_plus_fee_and_tax_exempt()
    {
        var order = MoneyOrderLineItem.Issue(new Money(500m), "Ana Ruiz", "Ben Ruiz");

        Assert.Equal(new Money(10m), order.Fee);
        Assert.Equal(new Money(510m), order.Total);
        Assert.Equal(TaxCategory.Exempt, order.TaxCategory);
    }

    [Fact]
    public void Money_order_needs_an_amount_names_and_one_currency()
    {
        Assert.Throws<DomainException>(() => MoneyOrderLineItem.Issue(new Money(0m), "Ana Ruiz", "Ben Ruiz"));
        Assert.Throws<DomainException>(() => MoneyOrderLineItem.Issue(new Money(50m), " ", "Ben Ruiz"));
        Assert.Throws<DomainException>(() =>
            new MoneyOrderLineItem(Guid.NewGuid(), new Money(50m), new Money(2m, "EUR"), "Ana Ruiz", "Ben Ruiz"));
    }
}
