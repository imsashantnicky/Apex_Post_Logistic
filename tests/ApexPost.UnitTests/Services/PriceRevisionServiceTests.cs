using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

public sealed class PriceRevisionServiceTests
{
    [Fact]
    public async Task Revision_changes_the_prices_in_one_transaction_and_refreshes_the_catalogue()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var unitOfWork = new FakeUnitOfWork();

        var changed = await harness.Prices(unitOfWork).ReviseAsync(Command(10m), Ct);

        Assert.Equal(3, changed);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(1, harness.Catalog.RefreshCount);
        var product = await harness.Catalog.FindAsync(new Sku(ServiceHarness.StandardSku), Ct);
        Assert.Equal(new Money(11.00m), product?.UnitPrice);
    }

    [Theory]
    [InlineData(StaffRole.CounterClerk)]
    [InlineData(StaffRole.BranchManager)]
    [InlineData(StaffRole.Auditor)]
    public async Task Only_an_administrator_may_revise_prices(StaffRole role)
    {
        using var harness = new ServiceHarness();
        harness.User.Role = role;
        var unitOfWork = new FakeUnitOfWork();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            harness.Prices(unitOfWork).ReviseAsync(Command(10m), Ct));

        Assert.Equal(0, unitOfWork.TransactionCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.001)]
    [InlineData(50.01)]
    [InlineData(-50.01)]
    public async Task Percent_outside_the_limits_is_refused(double percent)
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var unitOfWork = new FakeUnitOfWork();

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            harness.Prices(unitOfWork).ReviseAsync(Command((decimal)percent), Ct));

        Assert.Equal(0, harness.Catalog.RefreshCount);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(-50)]
    public async Task Percent_at_the_limit_is_accepted(int percent)
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);

        Assert.Equal(3, await harness.Prices(new FakeUnitOfWork()).ReviseAsync(Command(percent), Ct));
    }

    [Fact]
    public async Task A_reason_is_required()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            harness.Prices(new FakeUnitOfWork()).ReviseAsync(Command(5m) with { Reason = " " }, Ct));
    }

    private static PriceRevisionCommand Command(decimal percent) =>
        new() { Percent = percent, Reason = "Yearly price review." };
}
