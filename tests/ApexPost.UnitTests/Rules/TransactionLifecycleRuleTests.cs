using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rules 4 to 7 across the whole life of a transaction, where one step depends on an earlier one.</summary>
public sealed class TransactionLifecycleRuleTests
{
    private const string Reason = "Customer changed their mind.";

    [Fact]
    public void Void_returns_every_line_of_a_product_that_was_split_over_two_lines()
    {
        var stock = CreateStock(FirstSku, 5);
        var (transaction, _) = Paid(stock, 2, 1);

        transaction.Void(CreateStaff(StaffRole.BranchManager), Reason, [stock], Now);

        Assert.Equal(5, stock.Quantity);
    }

    [Fact]
    public void Refund_after_a_void_is_refused_and_returns_no_stock_twice()
    {
        var stock = CreateStock(FirstSku, 5);
        var (transaction, _) = Paid(stock, 2);
        var manager = CreateStaff(StaffRole.BranchManager);
        transaction.Void(manager, Reason, [stock], Now);

        Assert.Throws<DomainException>(() => transaction.Refund(manager, Reason, [stock], Now));

        Assert.Equal(5, stock.Quantity);
        Assert.Equal(TransactionStatus.Voided, transaction.Status);
    }

    [Fact]
    public void Second_refund_is_refused_and_returns_no_stock_twice()
    {
        var stock = CreateStock(FirstSku, 5);
        var (transaction, _) = Paid(stock, 2);
        var manager = CreateStaff(StaffRole.BranchManager);
        transaction.Refund(manager, Reason, [stock], Now);

        Assert.Throws<DomainException>(() => transaction.Refund(manager, Reason, [stock], Now));

        Assert.Equal(5, stock.Quantity);
    }

    [Fact]
    public void Refused_reversal_keeps_status_stock_and_events()
    {
        var stock = CreateStock(FirstSku, 5);
        var (transaction, clerk) = Paid(stock, 2);
        transaction.TakeEvents();

        Assert.ThrowsAny<DomainException>(() => transaction.Void(clerk, Reason, [stock], Now));

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(3, stock.Quantity);
        Assert.Null(transaction.Reversal);
        Assert.Empty(transaction.TakeEvents());
    }

    [Fact]
    public void Reversal_keeps_the_payment_and_its_frozen_tax()
    {
        var stock = CreateStock(FirstSku, 5);
        var (transaction, _) = Paid(stock, 1);
        var taxWhenPaid = transaction.Tax;

        transaction.Refund(CreateStaff(StaffRole.BranchManager), Reason, [stock], Now);

        Assert.Equal(taxWhenPaid, transaction.Tax);
        Assert.NotNull(transaction.Payment);
        Assert.Equal(Reason, transaction.Reversal?.Reason);
    }

    [Theory]
    [InlineData(TransactionStatus.Paid)]
    [InlineData(TransactionStatus.Voided)]
    public void Finished_transaction_takes_no_line_and_no_check(TransactionStatus status)
    {
        var stock = CreateStock(FirstSku, 5);
        var (transaction, clerk) = Paid(stock, 1);
        if (status == TransactionStatus.Voided)
        {
            transaction.Void(CreateStaff(StaffRole.BranchManager), Reason, [stock], Now);
        }

        Assert.Throws<DomainException>(() => transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(), 1)));
        Assert.Throws<DomainException>(() =>
            transaction.RecordVerification(clerk, CreateVerification(VerificationResult.Passed)));
    }

    [Fact]
    public void Inactive_clerk_cannot_record_a_check()
    {
        var clerk = CreateStaff();
        var transaction = OpenTransaction(CreateBranch(), clerk);
        clerk.Deactivate();

        Assert.ThrowsAny<DomainException>(() =>
            transaction.RecordVerification(clerk, CreateVerification(VerificationResult.Passed)));
        Assert.Empty(transaction.Verifications);
    }

    [Fact]
    public void A_passed_check_covers_every_money_order_of_the_transaction()
    {
        var (transaction, clerk) = OpenBasket();
        transaction.AddLine(clerk, MoneyOrderLineItem.Issue(new Money(300.01m), "Ana Ruiz", "Ben Ruiz"));
        transaction.AddLine(clerk, MoneyOrderLineItem.Issue(new Money(900m), "Ana Ruiz", "Ben Ruiz"));
        transaction.RecordVerification(clerk, CreateVerification(VerificationResult.Passed));

        transaction.Pay(clerk, PaymentMethod.Cash, CreateRules(), [], Now);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
    }

    [Fact]
    public void A_stored_check_never_shows_more_than_the_last_four_characters()
    {
        var check = CreateVerification(VerificationResult.Passed);

        Assert.Equal("****5678", check.MaskedNumber);
        Assert.DoesNotContain("1234", check.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ShipmentType.Letter, ServiceLevel.Standard, 2.001)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Express, 20.001)]
    public void Refused_shipment_never_reaches_the_basket(ShipmentType type, ServiceLevel level, double kilograms)
    {
        var (transaction, _) = OpenBasket();

        Assert.Throws<DomainException>(() => CreateShipment((decimal)kilograms, type, level));

        Assert.Empty(transaction.Lines);
    }

    [Fact]
    public void Paying_twice_is_refused_and_takes_the_stock_once()
    {
        var stock = CreateStock(FirstSku, 5);
        var (transaction, clerk) = Paid(stock, 2);

        Assert.Throws<DomainException>(() => transaction.Pay(clerk, PaymentMethod.Card, CreateRules(), [stock], Now));

        Assert.Equal(3, stock.Quantity);
    }

    private static (CounterTransaction Transaction, Staff Clerk) Paid(StockItem stock, params int[] quantities)
    {
        var (transaction, clerk) = OpenBasket();
        foreach (var quantity in quantities)
        {
            transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(), quantity));
        }

        transaction.Pay(clerk, PaymentMethod.Card, CreateRules(), [stock], Now);
        return (transaction, clerk);
    }
}
