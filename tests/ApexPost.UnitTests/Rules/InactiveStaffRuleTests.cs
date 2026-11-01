using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 2: inactive staff cannot open or process transactions, or sign in.</summary>
public sealed class InactiveStaffRuleTests
{
    [Fact]
    public void Inactive_clerk_cannot_open_a_transaction()
    {
        var clerk = CreateStaff(isActive: false);

        Assert.Throws<DomainException>(() => OpenTransaction(CreateBranch(), clerk));
    }

    [Fact]
    public void Clerk_deactivated_after_opening_cannot_process_the_basket()
    {
        var (transaction, clerk) = OpenBasket();
        transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(), 1));

        clerk.Deactivate();

        Assert.Throws<DomainException>(() => transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(), 1)));
        Assert.Throws<DomainException>(() => transaction.Pay(
            clerk, PaymentMethod.Cash, CreateRules(), [CreateStock(FirstSku, 10)], Now));
        Assert.Equal(TransactionStatus.Open, transaction.Status);
    }

    [Fact]
    public void Reactivated_clerk_can_transact_again()
    {
        var clerk = CreateStaff(isActive: false);

        clerk.Activate();

        Assert.True(clerk.CanTransact);
        Assert.Equal(TransactionStatus.Open, OpenTransaction(CreateBranch(), clerk).Status);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Only_active_staff_can_sign_in(bool isActive, bool expected)
    {
        Assert.Equal(expected, CreateStaff(isActive: isActive).CanSignIn);
    }
}
