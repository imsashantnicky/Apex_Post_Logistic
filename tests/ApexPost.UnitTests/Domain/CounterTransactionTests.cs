using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Domain;

public sealed class CounterTransactionTests
{
    [Fact]
    public void Open_starts_an_empty_basket_for_the_clerk()
    {
        var branch = CreateBranch();
        var clerk = CreateStaff();

        var transaction = OpenTransaction(branch, clerk);

        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(branch.Code, transaction.Branch);
        Assert.Equal(branch.Counters[0].Id, transaction.CounterId);
        Assert.Equal(clerk.Id, transaction.ClerkId);
        Assert.Equal(Now, transaction.OpenedAt);
        Assert.Equal(TransactionStatus.Open, transaction.Status);
        Assert.Empty(transaction.Lines);
        Assert.Equal(new Money(0m), transaction.Total);
    }

    [Fact]
    public void Basket_mixes_retail_shipping_and_money_orders()
    {
        var (transaction, clerk) = OpenBasket();

        transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(price: 10m), 2));
        transaction.AddLine(clerk, ShippingLineItem.Create(CreateShipment(2m), CreateZone(), Multipliers));
        transaction.AddLine(clerk, MoneyOrderLineItem.Issue(new Money(100m), "Ana Ruiz", "Ben Ruiz"));

        Assert.Equal(3, transaction.Lines.Count);
        Assert.Equal(new Money(130m), transaction.Subtotal);
    }

    [Fact]
    public void Same_line_cannot_be_added_twice()
    {
        var (transaction, clerk) = OpenBasket();
        var line = RetailLineItem.For(CreateProduct(), 1);

        transaction.AddLine(clerk, line);

        Assert.Throws<DomainException>(() => transaction.AddLine(clerk, line));
    }

    [Fact]
    public void Lines_in_another_currency_are_rejected()
    {
        var (transaction, clerk) = OpenBasket();
        transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(), 1));

        Assert.Throws<DomainException>(() =>
            transaction.AddLine(clerk, MoneyOrderLineItem.Issue(new Money(50m, "EUR"), "Ana Ruiz", "Ben Ruiz")));
    }

    [Fact]
    public void Empty_basket_cannot_be_paid()
    {
        var (transaction, clerk) = OpenBasket();

        Assert.Throws<DomainException>(() => transaction.Pay(clerk, PaymentMethod.Card, CreateRules(), [], Now));
    }

    [Fact]
    public void Paid_basket_can_no_longer_change()
    {
        var (transaction, clerk) = OpenBasket();
        transaction.AddLine(clerk, MoneyOrderLineItem.Issue(new Money(50m), "Ana Ruiz", "Ben Ruiz"));

        transaction.Pay(clerk, PaymentMethod.Card, CreateRules(), [], Now);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(new Payment(PaymentMethod.Card, new Money(0m), Now), transaction.Payment);
        Assert.Throws<DomainException>(() =>
            transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(), 1)));
        Assert.Throws<DomainException>(() =>
            transaction.RecordVerification(clerk, CreateVerification(VerificationResult.Passed)));
        Assert.Throws<DomainException>(() => transaction.Pay(clerk, PaymentMethod.Card, CreateRules(), [], Now));
    }

    [Fact]
    public void Restore_rebuilds_a_consistent_transaction()
    {
        var line = RetailLineItem.For(CreateProduct(), 1);
        var payment = new Payment(PaymentMethod.Cash, new Money(2m), Now.AddMinutes(5));
        var reversal = new Reversal(Guid.NewGuid(), "Customer changed mind", Now.AddMinutes(10));

        var transaction = Restore(TransactionStatus.Refunded, [line], payment, reversal);

        Assert.Equal(TransactionStatus.Refunded, transaction.Status);
        Assert.Equal(new Money(12m), transaction.Total);
        Assert.Equal(7, transaction.Version);
        Assert.Single(transaction.Verifications);
    }

    [Theory]
    [InlineData(TransactionStatus.Open, true, false)]
    [InlineData(TransactionStatus.Paid, false, false)]
    [InlineData(TransactionStatus.Paid, true, true)]
    [InlineData(TransactionStatus.Voided, true, false)]
    [InlineData(TransactionStatus.Refunded, false, true)]
    [InlineData((TransactionStatus)99, false, false)]
    public void Restore_rejects_a_status_that_does_not_match_the_data(
        TransactionStatus status,
        bool hasPayment,
        bool hasReversal)
    {
        var payment = hasPayment ? new Payment(PaymentMethod.Cash, new Money(2m), Now) : null;
        var reversal = hasReversal ? new Reversal(Guid.NewGuid(), "Wrong item", Now) : null;

        Assert.Throws<DomainException>(() =>
            Restore(status, [RetailLineItem.For(CreateProduct(), 1)], payment, reversal));
    }

    [Fact]
    public void Restore_rejects_events_out_of_time_order()
    {
        var line = RetailLineItem.For(CreateProduct(), 1);
        var paidTooEarly = new Payment(PaymentMethod.Cash, new Money(2m), Now.AddMinutes(-1));

        Assert.Throws<DomainException>(() => Restore(TransactionStatus.Paid, [line], paidTooEarly, null));
    }

    private static CounterTransaction Restore(
        TransactionStatus status,
        ILineItem[] lines,
        Payment? payment,
        Reversal? reversal) =>
        CounterTransaction.Restore(
            Guid.NewGuid(),
            new BranchCode(HomeBranch),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now,
            status,
            lines,
            [CreateVerification(VerificationResult.Passed)],
            payment,
            reversal,
            version: 7);
}
