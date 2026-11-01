using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 4: a money order above the threshold needs a passed identity check in the same transaction.</summary>
public sealed class MoneyOrderVerificationRuleTests
{
    [Theory]
    [InlineData(299.99)]
    [InlineData(300.00)]
    public void Money_order_up_to_the_threshold_needs_no_verification(double amount)
    {
        var (transaction, clerk) = BasketWithMoneyOrder((decimal)amount);

        Assert.False(transaction.RequiresVerification(new Money(300m)));
        transaction.Pay(clerk, PaymentMethod.Cash, CreateRules(threshold: 300m), [], Now);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
    }

    [Fact]
    public void Money_order_above_the_threshold_without_verification_is_rejected()
    {
        var (transaction, clerk) = BasketWithMoneyOrder(300.01m);

        Assert.True(transaction.RequiresVerification(new Money(300m)));
        Assert.Throws<DomainException>(() =>
            transaction.Pay(clerk, PaymentMethod.Cash, CreateRules(threshold: 300m), [], Now));
        Assert.Equal(TransactionStatus.Open, transaction.Status);
    }

    [Theory]
    [InlineData(VerificationResult.Failed)]
    [InlineData(VerificationResult.Pending)]
    public void Unsuccessful_verification_does_not_allow_payment(VerificationResult result)
    {
        var (transaction, clerk) = BasketWithMoneyOrder(300.01m);

        transaction.RecordVerification(clerk, CreateVerification(result));

        Assert.Throws<DomainException>(() =>
            transaction.Pay(clerk, PaymentMethod.Cash, CreateRules(threshold: 300m), [], Now));
    }

    [Fact]
    public void Passed_verification_allows_payment_above_the_threshold()
    {
        var (transaction, clerk) = BasketWithMoneyOrder(300.01m);

        transaction.RecordVerification(clerk, CreateVerification(VerificationResult.Failed));
        transaction.RecordVerification(clerk, CreateVerification(VerificationResult.Passed));
        transaction.Pay(clerk, PaymentMethod.Cash, CreateRules(threshold: 300m), [], Now);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(2, transaction.Verifications.Count);
    }

    [Fact]
    public void Threshold_comes_from_the_configured_rules()
    {
        var (transaction, clerk) = BasketWithMoneyOrder(400m);

        transaction.Pay(clerk, PaymentMethod.Cash, CreateRules(threshold: 500m), [], Now);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
    }

    private static (CounterTransaction Transaction, Staff Clerk) BasketWithMoneyOrder(decimal amount)
    {
        var (transaction, clerk) = OpenBasket();
        transaction.AddLine(clerk, MoneyOrderLineItem.Issue(new Money(amount), "Ana Ruiz", "Ben Ruiz"));
        return (transaction, clerk);
    }
}
