using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

public sealed class StockAdjustmentRecordTests
{
    [Fact]
    public async Task An_adjustment_keeps_a_permanent_record_with_the_new_quantity()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);
        var unitOfWork = new FakeUnitOfWork();
        var stock = harness.StockRecords(unitOfWork);

        await harness.StockAdjustments(stock, unitOfWork).AdjustAsync(Command(-4), Ct);

        var record = Assert.Single(stock.Adjustments);
        Assert.Equal(-4, record.Change);
        Assert.Equal(ServiceHarness.InitialStock - 4, record.NewQuantity);
        Assert.Equal("Damaged in the store room.", record.Reason);
    }

    [Fact]
    public async Task A_refused_adjustment_leaves_no_record()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Clerk);
        var unitOfWork = new FakeUnitOfWork();
        var stock = harness.StockRecords(unitOfWork);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            harness.StockAdjustments(stock, unitOfWork).AdjustAsync(Command(1), Ct));

        Assert.Empty(stock.Adjustments);
    }

    private static StockAdjustmentCommand Command(int change) =>
        new() { Sku = ServiceHarness.StandardSku, Change = change, Reason = "Damaged in the store room." };
}
