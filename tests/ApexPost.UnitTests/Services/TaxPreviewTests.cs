using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Options;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

/// <summary>Rule 9: tax is previewed on the open basket and frozen at payment.</summary>
public sealed class TaxPreviewTests
{
    [Fact]
    public async Task Open_basket_shows_the_tax_preview_not_zero()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku);
        await checkout.AddRetailAsync(id, ServiceHarness.ReducedSku);
        await checkout.AddRetailAsync(id, ServiceHarness.ExemptSku);

        var details = await checkout.GetAsync(id, Ct);

        Assert.Equal(TransactionStatus.Open, details.Status);
        Assert.Equal(new Money(16.50m), details.Subtotal);
        Assert.Equal(new Money(2.20m), details.Tax);
        Assert.Equal(new Money(18.70m), details.Total);
    }

    [Fact]
    public async Task Paid_basket_keeps_the_tax_that_was_frozen_at_payment()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku);
        await checkout.PayAsync(id, PaymentMethod.Card, Ct);
        var higherRates = new TaxCalculator(Options.Create(new TaxSettings { StandardPercent = 25m }));

        var transaction = await harness.TransactionAsync(id);

        Assert.Equal(new Money(2.00m), transaction.Tax);
        Assert.Equal(new Money(2.00m), higherRates.Preview(transaction));
        Assert.Equal(new Money(12.00m), (await checkout.GetAsync(id, Ct)).Total);
    }

    [Fact]
    public async Task Shipping_is_taxed_at_standard_rate_and_money_orders_are_exempt()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddParcelAsync(id, 2m);
        await checkout.AddMoneyOrderAsync(id, 100m);

        var details = await checkout.GetAsync(id, Ct);

        Assert.Equal(new Money(1.60m), details.Tax);
    }

    [Fact]
    public async Task Transaction_list_shows_totals_with_the_tax_preview()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku);

        var list = await checkout.ListAsync(new(), new(), Ct);

        Assert.Equal(new Money(12.00m), Assert.Single(list.Items).Total);
    }
}
