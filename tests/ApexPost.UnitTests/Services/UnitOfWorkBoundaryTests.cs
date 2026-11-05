using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Services;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

/// <summary>
/// The transaction boundary of every write: a use case that succeeds commits exactly once, and one that fails commits
/// nothing. Fake repositories only stage, so a missing commit would lose the change and a second one would split it.
/// </summary>
public sealed class UnitOfWorkBoundaryTests
{
    private const string Password = "A-long-enough-password";
    private const string Reason = "Customer changed their mind.";

    private static readonly ReversalCommand Reversal = new() { Reason = Reason };

    [Fact]
    public async Task Opening_a_transaction_commits_once()
    {
        using var harness = new ServiceHarness();

        await harness.OpenAsync();

        Assert.Equal(1, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Opening_at_a_closed_branch_commits_nothing()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.ClosedBranchClerk);

        await Assert.ThrowsAsync<DomainException>(() =>
            harness.Checkout().OpenAsync(harness.ClosedBranch.Counters[0].Id, Ct));

        Assert.Equal(0, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Each_basket_step_commits_once()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();
        var checkout = harness.Checkout();

        await AssertOneCommitAsync(harness, () => checkout.AddRetailAsync(id, ServiceHarness.StandardSku));
        await AssertOneCommitAsync(harness, () => harness.Checkout().AddParcelAsync(id, 2m));
        await AssertOneCommitAsync(harness, () => harness.Checkout().AddMoneyOrderAsync(id, 50m));
        await AssertOneCommitAsync(harness, () => harness.Checkout().VerifyAsync(id));
    }

    [Fact]
    public async Task Refused_basket_steps_commit_nothing()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();
        var checkout = harness.Checkout();

        await AssertNoCommitAsync<NotFoundException>(harness, () => checkout.AddRetailAsync(id, "PKG-9999"));
        await AssertNoCommitAsync<DomainException>(
            harness,
            () => checkout.AddParcelAsync(id, 21m, ServiceLevel.Express));
        await AssertNoCommitAsync<Exception>(harness, () => checkout.AddMoneyOrderAsync(id, -1m));
        await AssertNoCommitAsync<ValidationFailedException>(
            harness,
            () => checkout.VerifyIdentityAsync(id, new VerifyIdentityCommand(), Ct));
    }

    [Fact]
    public async Task Payment_with_stock_commits_once()
    {
        using var harness = new ServiceHarness();
        var id = await BasketAsync(harness, 3);

        await AssertOneCommitAsync(harness, () => harness.Checkout().PayAsync(id, PaymentMethod.Card, Ct));

        Assert.Equal(ServiceHarness.InitialStock - 3, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Payment_short_of_stock_commits_nothing()
    {
        using var harness = new ServiceHarness();
        var id = await BasketAsync(harness, ServiceHarness.InitialStock + 1);

        await AssertNoCommitAsync<InsufficientStockException>(
            harness,
            () => harness.Checkout().PayAsync(id, PaymentMethod.Card, Ct));
    }

    [Fact]
    public async Task Payment_without_the_needed_verification_commits_nothing()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();
        await harness.Checkout().AddMoneyOrderAsync(id, 300.01m);

        await AssertNoCommitAsync<DomainException>(
            harness,
            () => harness.Checkout().PayAsync(id, PaymentMethod.Cash, Ct));
    }

    [Fact]
    public async Task A_payment_whose_commit_fails_changes_nothing_and_tells_nobody()
    {
        using var harness = new ServiceHarness();
        var id = await BasketAsync(harness, 3);
        harness.UnitOfWork.FailNextCommitWith(Conflict());

        await AssertNoCommitAsync<ConcurrencyConflictException>(
            harness,
            () => harness.Checkout().PayAsync(id, PaymentMethod.Card, Ct));

        Assert.Equal(1, harness.UnitOfWork.FailedCommits);
        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));
        Assert.Equal(TransactionStatus.Open, (await harness.TransactionAsync(id)).Status);
        Assert.Empty(harness.Sink.Published);
    }

    [Fact]
    public async Task Void_and_refund_commit_once()
    {
        using var harness = new ServiceHarness();
        var voided = await PaidAsync(harness, 1);
        var refunded = await PaidAsync(harness, 1);
        harness.As(harness.Manager);

        await AssertOneCommitAsync(harness, () => harness.Reversal().VoidAsync(voided, Reversal, Ct));
        await AssertOneCommitAsync(harness, () => harness.Reversal().RefundAsync(refunded, Reversal, Ct));

        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Refused_reversals_commit_nothing()
    {
        using var harness = new ServiceHarness();
        var paid = await PaidAsync(harness, 1);
        var open = await BasketAsync(harness, 1);

        await AssertNoCommitAsync<ForbiddenException>(
            harness,
            () => harness.Reversal().VoidAsync(paid, Reversal, Ct));
        harness.As(harness.OtherManager);
        await AssertNoCommitAsync<ForbiddenException>(
            harness,
            () => harness.Reversal().RefundAsync(paid, Reversal, Ct));
        harness.As(harness.Manager);
        await AssertNoCommitAsync<ValidationFailedException>(
            harness,
            () => harness.Reversal().VoidAsync(paid, new ReversalCommand(), Ct));
        await AssertNoCommitAsync<DomainException>(
            harness,
            () => harness.Reversal().RefundAsync(open, Reversal, Ct));
    }

    [Fact]
    public async Task A_void_whose_commit_fails_keeps_the_sale_and_the_stock()
    {
        using var harness = new ServiceHarness();
        var id = await PaidAsync(harness, 2);
        harness.As(harness.Manager);
        var published = harness.Sink.Published.Count;
        harness.UnitOfWork.FailNextCommitWith(Conflict());

        await AssertNoCommitAsync<ConcurrencyConflictException>(
            harness,
            () => harness.Reversal().VoidAsync(id, Reversal, Ct));

        Assert.Equal(TransactionStatus.Paid, (await harness.TransactionAsync(id)).Status);
        Assert.Equal(ServiceHarness.InitialStock - 2, await harness.StockOfAsync(ServiceHarness.StandardSku));
        Assert.Equal(published, harness.Sink.Published.Count);
    }

    [Fact]
    public async Task Creating_staff_commits_once_and_a_taken_email_commits_nothing()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var details = new StaffCommand
        {
            Name = "Sam Taylor",
            Email = "new.clerk@apexpost.test",
            HomeBranch = harness.Branch.Code.ToString(),
        };
        var password = new PasswordCommand { Password = Password };

        await AssertOneCommitAsync(harness, () => harness.StaffAdmin().CreateAsync(details, password, Ct));
        await AssertNoCommitAsync<ValidationFailedException>(
            harness,
            () => harness.StaffAdmin().CreateAsync(details, password, Ct));
    }

    [Fact]
    public async Task Closing_a_branch_commits_once_and_closing_it_again_commits_nothing()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var branch = await harness.BranchAdmin().GetAsync(harness.Branch.Code, Ct);

        await AssertOneCommitAsync(harness, () => harness.BranchAdmin().CloseAsync(branch.Code, branch.Version, Ct));
        var closed = await harness.BranchAdmin().GetAsync(harness.Branch.Code, Ct);
        await AssertNoCommitAsync<DomainException>(
            harness,
            () => harness.BranchAdmin().CloseAsync(closed.Code, closed.Version, Ct));
    }

    [Fact]
    public async Task A_price_revision_commits_once_inside_one_transaction()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var command = new PriceRevisionCommand { Category = ProductCategory.Packaging, Percent = 5m, Reason = Reason };

        await AssertOneCommitAsync(harness, () => harness.Prices().ReviseAsync(command, Ct));
    }

    [Fact]
    public async Task A_stock_adjustment_commits_in_the_procedure_and_not_through_the_unit_of_work()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);
        var command = new StockAdjustmentCommand { Sku = ServiceHarness.StandardSku, Change = -1, Reason = Reason };

        await harness.StockAdjustments().AdjustAsync(command, Ct);

        Assert.Equal(0, harness.UnitOfWork.Commits);
        Assert.Equal(ServiceHarness.InitialStock - 1, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    private static ConcurrencyConflictException Conflict() => new("Changed.", new InvalidOperationException());

    private static async Task<Guid> BasketAsync(ServiceHarness harness, int quantity)
    {
        harness.As(harness.Clerk);
        var id = await harness.OpenAsync();
        await harness.Checkout().AddRetailAsync(id, ServiceHarness.StandardSku, quantity);
        return id;
    }

    private static async Task<Guid> PaidAsync(ServiceHarness harness, int quantity)
    {
        var id = await BasketAsync(harness, quantity);
        await harness.Checkout().PayAsync(id, PaymentMethod.Card, Ct);
        return id;
    }

    private static async Task AssertOneCommitAsync(ServiceHarness harness, Func<Task> useCase)
    {
        var before = harness.UnitOfWork.Commits;

        await useCase();

        Assert.Equal(before + 1, harness.UnitOfWork.Commits);
    }

    private static async Task AssertNoCommitAsync<TException>(ServiceHarness harness, Func<Task> useCase)
        where TException : Exception
    {
        var before = harness.UnitOfWork.Commits;

        await Assert.ThrowsAnyAsync<TException>(useCase);

        Assert.Equal(before, harness.UnitOfWork.Commits);
    }
}
