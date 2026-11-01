using System.Security.Claims;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using ApexPost.UnitTests.Support;

namespace ApexPost.UnitTests.Services;

public sealed class SpecificationAndExtensionTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(TestData.Now.UtcDateTime);

    [Fact]
    public void Specifications_combine_with_and()
    {
        var even = new PredicateSpecification<int>(number => number % 2 == 0);
        var large = new PredicateSpecification<int>(number => number > 10);

        var both = even.And(large);

        Assert.True(both.IsSatisfiedBy(12));
        Assert.False(both.IsSatisfiedBy(8));
        Assert.False(both.IsSatisfiedBy(13));
    }

    [Fact]
    public void Report_filter_matches_branch_status_and_date_range()
    {
        var (transaction, _) = TestData.OpenBasket();
        var home = new BranchCode(TestData.HomeBranch);

        Assert.True(ReportSpecifications.Transactions(new(home, Today, Today)).IsSatisfiedBy(transaction));
        Assert.True(ReportSpecifications.Transactions(new(null, Today, Today)).IsSatisfiedBy(transaction));
        Assert.False(ReportSpecifications.Transactions(new(new BranchCode(TestData.OtherBranch), Today, Today))
            .IsSatisfiedBy(transaction));
        Assert.False(ReportSpecifications.Transactions(new(home, Today.AddDays(1), Today.AddDays(2)))
            .IsSatisfiedBy(transaction));
        Assert.False(ReportSpecifications.PaidTransactions(new(home, Today, Today)).IsSatisfiedBy(transaction));
        Assert.True(ReportSpecifications.Matching(new TransactionFilter(Status: TransactionStatus.Open))
            .IsSatisfiedBy(transaction));
    }

    [Fact]
    public void Basket_summary_splits_retail_from_services()
    {
        var (transaction, clerk) = TestData.OpenBasket();
        transaction.AddLine(clerk, RetailLineItem.For(TestData.CreateProduct(), 2));
        transaction.AddLine(clerk, MoneyOrderLineItem.Issue(new Money(100m), "Ann", "Bo"));

        Assert.Equal(new Money(20m), transaction.RetailTotal);
        Assert.Equal(new Money(102m), transaction.ServicesTotal);
        Assert.Null(transaction.VerificationSummary);
    }

    [Fact]
    public void Claims_are_read_as_typed_values()
    {
        var id = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ApexClaimTypes.StaffId, id.ToString()),
            new Claim(ClaimTypes.Role, nameof(StaffRole.BranchManager)),
            new Claim(ApexClaimTypes.BranchCode, "br001"),
        ]));

        Assert.Equal(id, principal.StaffId);
        Assert.Equal(StaffRole.BranchManager, principal.Role);
        Assert.Equal(new BranchCode(TestData.HomeBranch), principal.BranchCode);
    }

    [Fact]
    public void Missing_or_invalid_claims_are_null()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ApexClaimTypes.StaffId, "not-a-guid"),
            new Claim(ClaimTypes.Role, "99"),
            new Claim(ApexClaimTypes.BranchCode, "XX1"),
        ]));

        Assert.Null(principal.StaffId);
        Assert.Null(principal.Role);
        Assert.Null(principal.BranchCode);
    }

    [Fact]
    public void Branch_scope_depends_on_the_role()
    {
        var home = new BranchCode(TestData.HomeBranch);
        var other = new BranchCode(TestData.OtherBranch);
        var clerk = new FakeCurrentUser { Role = StaffRole.CounterClerk, Branch = home };
        var auditor = new FakeCurrentUser { Role = StaffRole.Auditor, Branch = home };

        Assert.Equal(home, clerk.ScopeTo(null));
        Assert.Equal(home, clerk.ScopeTo(home));
        Assert.Throws<ForbiddenException>(() => clerk.ScopeTo(other));
        Assert.Null(auditor.ScopeTo(null));
        Assert.Equal(other, auditor.ScopeTo(other));
    }

    [Fact]
    public void Paging_cuts_a_page_and_counts_all_items()
    {
        int[] items = [.. Enumerable.Range(1, 45)];

        var page = items.ToPage(new PageRequest(3, 20));

        Assert.Equal([41, 42, 43, 44, 45], page.Items);
        Assert.Equal((45, 3, false, true), (page.TotalCount, page.TotalPages, page.HasNext, page.HasPrevious));
        Assert.Equal(PageRequest.MaximumSize, new PageRequest(0, 1000).Size);
    }
}
