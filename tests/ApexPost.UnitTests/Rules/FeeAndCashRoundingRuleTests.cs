using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Transactions;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 10: money order fee = max(2.00, 2 % of amount); cash totals round to the nearest 0.05.</summary>
public sealed class FeeAndCashRoundingRuleTests
{
    [Theory]
    [InlineData(50.00, 2.00)]
    [InlineData(99.99, 2.00)]
    [InlineData(100.00, 2.00)]
    [InlineData(100.50, 2.01)]
    [InlineData(500.00, 10.00)]
    public void Fee_is_two_percent_with_a_minimum_of_two(double amount, double expected)
    {
        Assert.Equal(new Money((decimal)expected), MoneyOrderFeeRule.FeeFor(new Money((decimal)amount)));
    }

    [Theory]
    [InlineData(10.00, 10.00)]
    [InlineData(10.02, 10.00)]
    [InlineData(10.03, 10.05)]
    [InlineData(10.07, 10.05)]
    [InlineData(10.08, 10.10)]
    public void Cash_amount_rounds_to_the_nearest_five_cents(double amount, double expected)
    {
        Assert.Equal(new Money((decimal)expected), new Money((decimal)amount).RoundedForCash);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, 10.05)]
    [InlineData(PaymentMethod.Card, 10.03)]
    public void Only_a_cash_payment_rounds_the_amount_due(PaymentMethod method, double expected)
    {
        var (transaction, clerk) = OpenBasket();
        var exemptProduct = CreateProduct(price: 10.03m, taxCategory: TaxCategory.Exempt);
        transaction.AddLine(clerk, RetailLineItem.For(exemptProduct, 1));

        transaction.Pay(clerk, method, CreateRules(), [CreateStock(FirstSku, 5)], Now);

        Assert.Equal(new Money(10.03m), transaction.Total);
        Assert.Equal(new Money((decimal)expected), transaction.AmountDue);
    }
}
