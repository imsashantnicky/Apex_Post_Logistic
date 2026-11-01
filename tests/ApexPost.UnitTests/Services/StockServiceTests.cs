using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Inventory;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

public sealed class StockServiceTests
{
    [Theory]
    [InlineData(5, 15)]
    [InlineData(-10, 0)]
    public async Task Manager_adjusts_the_stock_of_the_own_branch(int change, int expected)
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);

        var result = await harness.StockAdjustments().AdjustAsync(Adjustment(ServiceHarness.StandardSku, change), Ct);

        Assert.Equal(expected, result.NewQuantity);
        Assert.Equal(expected, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Stock_never_goes_below_zero()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            harness.StockAdjustments().AdjustAsync(Adjustment(ServiceHarness.StandardSku, -11), Ct));

        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Reducing_to_the_threshold_sends_a_low_stock_notification()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);

        await harness.StockAdjustments().AdjustAsync(Adjustment(ServiceHarness.StandardSku, -5), Ct);

        Assert.Equal(NotificationKind.LowStock, Assert.Single(harness.Sink.Published).Kind);
    }

    [Fact]
    public async Task Clerk_cannot_adjust_stock()
    {
        using var harness = new ServiceHarness();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            harness.StockAdjustments().AdjustAsync(Adjustment(ServiceHarness.StandardSku, 1), Ct));
    }

    [Fact]
    public async Task Zero_change_and_missing_reason_are_invalid()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);
        var service = harness.StockAdjustments();

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            service.AdjustAsync(Adjustment(ServiceHarness.StandardSku, 0), Ct));
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            service.AdjustAsync(Adjustment(ServiceHarness.StandardSku, 1) with { Reason = " " }, Ct));
    }

    [Fact]
    public async Task Unknown_product_is_not_found()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.StockAdjustments().AdjustAsync(Adjustment("PKG-9999", 1), Ct));
    }

    [Fact]
    public async Task First_delivery_to_another_branch_creates_its_stock()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.OtherManager);

        await harness.StockAdjustments().AdjustAsync(Adjustment(ServiceHarness.ReducedSku, 7), Ct);

        Assert.Equal(7, await harness.StockOfAsync(ServiceHarness.ReducedSku, TestData.OtherBranch));
        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.ReducedSku));
    }

    [Fact]
    public async Task Catalogue_shows_branch_stock_and_low_flag()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);
        await harness.StockAdjustments().AdjustAsync(Adjustment(ServiceHarness.ExemptSku, -6), Ct);

        var items = await harness.Catalogue().SearchAsync(new CatalogueQuery("off"), Ct);

        var item = Assert.Single(items);
        Assert.Equal(4, item.InStock);
        Assert.True(item.IsLow);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            harness.Catalogue().SearchAsync(new CatalogueQuery(Branch: harness.OtherBranch.Code), Ct));
    }

    private static StockAdjustmentCommand Adjustment(string sku, int change) =>
        new() { Sku = sku, Change = change, Reason = "Delivery count" };
}
