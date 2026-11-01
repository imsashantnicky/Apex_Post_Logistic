using ApexPost.Domain.Common;
using ApexPost.Domain.Events;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 11: low stock (quantity at or below the threshold), money orders and reversals raise events.</summary>
public sealed class NotificationEventRuleTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Low_stock_means_at_or_below_the_threshold(int quantity, bool expected)
    {
        var isLow = LowStockRule.AtOrBelow(5);

        Assert.Equal(expected, isLow(CreateStock(FirstSku, quantity)));
    }

    [Fact]
    public void Negative_low_stock_threshold_is_rejected()
    {
        Assert.Throws<DomainException>(() => LowStockRule.AtOrBelow(-1));
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, false)]
    public void Sale_that_leaves_low_stock_raises_StockLow(int startQuantity, bool expected)
    {
        var (transaction, clerk) = OpenBasket();
        transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(), 2));
        var stock = CreateStock(FirstSku, startQuantity);

        transaction.Pay(clerk, PaymentMethod.Card, CreateRules(lowStockThreshold: 5), [stock], Now);

        var stockLow = transaction.TakeEvents().OfType<StockLow>().SingleOrDefault();
        Assert.Equal(expected, stockLow is not null);
        Assert.Equal(expected ? 5 : null, stockLow?.Quantity);
    }

    [Fact]
    public void Payment_raises_completed_and_one_event_per_money_order()
    {
        var (transaction, clerk) = OpenBasket();
        var order = MoneyOrderLineItem.Issue(new Money(1500m), "Ana Ruiz", "Ben Ruiz");
        transaction.AddLine(clerk, order);
        transaction.AddLine(clerk, MoneyOrderLineItem.Issue(new Money(20m), "Ana Ruiz", "Cy Ruiz"));
        transaction.RecordVerification(clerk, CreateVerification(VerificationResult.Passed));

        transaction.Pay(clerk, PaymentMethod.Cash, CreateRules(), [], Now);

        var events = transaction.TakeEvents();
        var completed = Assert.Single(events.OfType<TransactionCompleted>());
        Assert.Equal(transaction.Total, completed.Total);
        Assert.Equal(PaymentMethod.Cash, completed.Method);
        Assert.Equal(2, events.OfType<MoneyOrderIssued>().Count());
        Assert.Contains(events.OfType<MoneyOrderIssued>(), issued => issued.LineId == order.Id);
        Assert.Empty(transaction.TakeEvents());
    }

    [Fact]
    public void Void_and_refund_raise_their_own_event()
    {
        var manager = CreateStaff(StaffRole.BranchManager);
        var voided = PaidMoneyOrder(manager);
        var refunded = PaidMoneyOrder(manager);

        voided.Void(manager, "Wrong recipient", [], Now);
        refunded.Refund(manager, "Customer request", [], Now);

        var voidEvent = Assert.IsType<TransactionVoided>(Assert.Single(voided.TakeEvents()));
        var refundEvent = Assert.IsType<TransactionRefunded>(Assert.Single(refunded.TakeEvents()));
        Assert.Equal("Wrong recipient", voidEvent.Reason);
        Assert.Equal(manager.Id, refundEvent.ManagerId);
    }

    private static CounterTransaction PaidMoneyOrder(Staff clerk)
    {
        var transaction = OpenTransaction(CreateBranch(), clerk);
        transaction.AddLine(clerk, MoneyOrderLineItem.Issue(new Money(50m), "Ana Ruiz", "Ben Ruiz"));
        transaction.Pay(clerk, PaymentMethod.Card, CreateRules(), [], Now);
        transaction.TakeEvents();
        return transaction;
    }
}
