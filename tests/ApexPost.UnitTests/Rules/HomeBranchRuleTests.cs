using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 3: staff transact only at their home branch; auditors and administrators never transact.</summary>
public sealed class HomeBranchRuleTests
{
    [Theory]
    [InlineData(StaffRole.CounterClerk)]
    [InlineData(StaffRole.BranchManager)]
    public void Clerk_and_manager_can_transact_at_their_home_branch(StaffRole role)
    {
        var staff = CreateStaff(role);

        Assert.True(staff.CanTransact);
        Assert.Equal(TransactionStatus.Open, OpenTransaction(CreateBranch(), staff).Status);
    }

    [Fact]
    public void Clerk_cannot_transact_at_another_branch()
    {
        var clerk = CreateStaff(branch: OtherBranch);

        Assert.Throws<DomainException>(() => OpenTransaction(CreateBranch(HomeBranch), clerk));
    }

    [Theory]
    [InlineData(StaffRole.Auditor)]
    [InlineData(StaffRole.Administrator)]
    public void Auditor_and_administrator_cannot_transact(StaffRole role)
    {
        var staff = CreateStaff(role);

        Assert.False(staff.CanTransact);
        Assert.Throws<DomainException>(() => OpenTransaction(CreateBranch(), staff));
    }

    [Fact]
    public void Clerk_moved_to_another_branch_cannot_continue_an_old_basket()
    {
        var (transaction, clerk) = OpenBasket();

        clerk.HomeBranch = new BranchCode(OtherBranch);

        Assert.Throws<DomainException>(() => transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(), 1)));
    }

    [Fact]
    public void Counter_of_another_branch_cannot_be_used()
    {
        var branch = CreateBranch();
        var otherCounter = CreateBranch(OtherBranch).Counters[0];

        Assert.Throws<DomainException>(() => CounterTransaction.Open(branch, otherCounter, CreateStaff(), Now));
    }
}
