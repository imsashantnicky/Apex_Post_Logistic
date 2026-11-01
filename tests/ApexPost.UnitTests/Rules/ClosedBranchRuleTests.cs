using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 1: a closed branch cannot accept transactions.</summary>
public sealed class ClosedBranchRuleTests
{
    [Fact]
    public void Open_branch_accepts_a_transaction()
    {
        Assert.Equal(TransactionStatus.Open, OpenTransaction(CreateBranch(), CreateStaff()).Status);
    }

    [Fact]
    public void Closed_branch_rejects_a_transaction()
    {
        var branch = CreateBranch(isActive: false);

        Assert.Throws<DomainException>(() => OpenTransaction(branch, CreateStaff()));
    }

    [Fact]
    public void Branch_accepts_transactions_again_after_reopening()
    {
        var branch = CreateBranch();
        var clerk = CreateStaff();

        branch.Close();
        Assert.Throws<DomainException>(() => OpenTransaction(branch, clerk));
        branch.Reopen();

        Assert.Equal(TransactionStatus.Open, OpenTransaction(branch, clerk).Status);
    }

    [Fact]
    public void Branch_closes_only_when_open_and_reopens_only_when_closed()
    {
        var branch = CreateBranch();

        Assert.Throws<DomainException>(branch.Reopen);
        branch.Close();
        Assert.Throws<DomainException>(branch.Close);
    }
}
