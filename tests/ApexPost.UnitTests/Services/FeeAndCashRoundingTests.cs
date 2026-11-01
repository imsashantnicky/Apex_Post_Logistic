using ApexPost.Application.DTOs;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

/// <summary>Rule 10: fee = max(2.00, 2 % of the amount); a cash total is rounded to the nearest 0.05.</summary>
public sealed class FeeAndCashRoundingTests
{
    [Theory]
    [InlineData(50, 2.00)]
    [InlineData(100, 2.00)]
    [InlineData(100.50, 2.01)]
    [InlineData(250, 5.00)]
    public async Task Money_order_line_adds_the_fee(double amount, double fee)
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();

        await checkout.AddMoneyOrderAsync(id, (decimal)amount);

        var line = Assert.Single((await checkout.GetAsync(id, Ct)).Lines);
        Assert.Equal(LineKind.MoneyOrder, line.Kind);
        Assert.Equal(new Money((decimal)(amount + fee)), line.Total);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, 13.45)]
    [InlineData(PaymentMethod.Card, 13.44)]
    public async Task Only_cash_payments_are_rounded_to_five_cents(PaymentMethod method, double amountDue)
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddParcelAsync(id, 1.333m, ServiceLevel.Express);

        await checkout.PayAsync(id, method, Ct);

        var details = await checkout.GetAsync(id, Ct);
        Assert.Equal(new Money(13.44m), details.Total);
        Assert.Equal(new Money((decimal)amountDue), details.AmountDue);
    }
}
