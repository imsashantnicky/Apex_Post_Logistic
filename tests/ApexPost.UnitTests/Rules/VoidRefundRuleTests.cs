using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 5: voids and refunds need an active manager of the branch and a reason; stock comes back.</summary>
public sealed class VoidRefundRuleTests
{
    private const string Reason = "Customer changed mind";

    private readonly Staff _manager = CreateStaff(StaffRole.BranchManager);
    private readonly StockItem _stock = CreateStock(FirstSku, 10);

    [Fact]
    public void Manager_voids_an_open_transaction_without_touching_stock()
    {
        var transaction = OpenWithTwoItems();

        transaction.Void(_manager, Reason, [_stock], Now);

        Assert.Equal(TransactionStatus.Voided, transaction.Status);
        Assert.Equal(new Reversal(_manager.Id, Reason, Now), transaction.Reversal);
        Assert.Equal(10, _stock.Quantity);
    }

    [Fact]
    public void Voiding_a_paid_transaction_returns_the_stock()
    {
        var transaction = PaidWithTwoItems();
        Assert.Equal(8, _stock.Quantity);

        transaction.Void(_manager, Reason, [_stock], Now);

        Assert.Equal(TransactionStatus.Voided, transaction.Status);
        Assert.Equal(10, _stock.Quantity);
    }

    [Fact]
    public void Refunding_a_paid_transaction_returns_the_stock()
    {
        var transaction = PaidWithTwoItems();

        transaction.Refund(_manager, Reason, [_stock], Now);

        Assert.Equal(TransactionStatus.Refunded, transaction.Status);
        Assert.Equal(10, _stock.Quantity);
    }

    [Theory]
    [InlineData(StaffRole.CounterClerk)]
    [InlineData(StaffRole.Auditor)]
    [InlineData(StaffRole.Administrator)]
    public void Only_a_branch_manager_can_void_or_refund(StaffRole role)
    {
        var transaction = PaidWithTwoItems();
        var staff = CreateStaff(role);

        Assert.Throws<DomainException>(() => transaction.Void(staff, Reason, [_stock], Now));
        Assert.Throws<DomainException>(() => transaction.Refund(staff, Reason, [_stock], Now));
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(8, _stock.Quantity);
    }

    [Fact]
    public void Manager_of_another_branch_cannot_void()
    {
        var transaction = PaidWithTwoItems();
        var otherManager = CreateStaff(StaffRole.BranchManager, OtherBranch);

        Assert.Throws<DomainException>(() => transaction.Void(otherManager, Reason, [_stock], Now));
    }

    [Fact]
    public void Inactive_manager_cannot_refund()
    {
        var transaction = PaidWithTwoItems();
        _manager.Deactivate();

        Assert.Throws<DomainException>(() => transaction.Refund(_manager, Reason, [_stock], Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Void_and_refund_need_a_reason(string reason)
    {
        var transaction = PaidWithTwoItems();

        Assert.Throws<DomainException>(() => transaction.Void(_manager, reason, [_stock], Now));
        Assert.Throws<DomainException>(() => transaction.Refund(_manager, reason, [_stock], Now));
        Assert.Equal(8, _stock.Quantity);
    }

    [Fact]
    public void Reason_has_a_maximum_length()
    {
        var atLimit = new string('r', TextLimits.Reason);

        Assert.Throws<DomainException>(() => PaidWithTwoItems().Void(_manager, atLimit + "r", [_stock], Now));
        PaidWithTwoItems().Void(_manager, atLimit, [_stock], Now);
    }

    [Fact]
    public void Only_a_paid_transaction_can_be_refunded()
    {
        var open = OpenWithTwoItems();
        var voided = PaidWithTwoItems();
        voided.Void(_manager, Reason, [_stock], Now);

        Assert.Throws<DomainException>(() => open.Refund(_manager, Reason, [_stock], Now));
        Assert.Throws<DomainException>(() => voided.Refund(_manager, Reason, [_stock], Now));
    }

    [Fact]
    public void Voided_or_refunded_transaction_cannot_be_voided()
    {
        var voided = PaidWithTwoItems();
        voided.Void(_manager, Reason, [_stock], Now);
        var refunded = PaidWithTwoItems();
        refunded.Refund(_manager, Reason, [_stock], Now);

        Assert.Throws<DomainException>(() => voided.Void(_manager, Reason, [_stock], Now));
        Assert.Throws<DomainException>(() => refunded.Void(_manager, Reason, [_stock], Now));
    }

    [Fact]
    public void Missing_stock_record_stops_the_refund_before_anything_changes()
    {
        var transaction = PaidWithTwoItems();

        Assert.Throws<DomainException>(() => transaction.Refund(_manager, Reason, [], Now));
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
    }

    private static CounterTransaction OpenWithTwoItems()
    {
        var (transaction, clerk) = OpenBasket();
        transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(FirstSku), 2));
        return transaction;
    }

    private CounterTransaction PaidWithTwoItems()
    {
        var clerk = CreateStaff();
        var transaction = OpenTransaction(CreateBranch(), clerk);
        transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(FirstSku), 2));
        transaction.Pay(clerk, PaymentMethod.Card, CreateRules(), [_stock], Now);
        return transaction;
    }
}
