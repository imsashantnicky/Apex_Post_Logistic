using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

public sealed class ReportServiceTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(TestData.Now.UtcDateTime);

    [Fact]
    public async Task Daily_sales_are_split_into_retail_and_services()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        harness.As(harness.Manager);

        var rows = await harness.Reports().DailySalesAsync(TodayFor(null), Ct);

        var row = Assert.Single(rows);
        Assert.Equal(1, row.Transactions);
        Assert.Equal(new Money(10.00m), row.Retail);
        Assert.Equal(new Money(60.00m), row.Services);
        Assert.Equal(new Money(3.60m), row.Tax);
        Assert.Equal(new Money(73.60m), row.Total);
    }

    [Fact]
    public async Task Shipping_volume_is_grouped_by_zone_and_level()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        harness.As(harness.Auditor);

        var row = Assert.Single(await harness.Reports().ShippingVolumeAsync(TodayFor(null), Ct));

        Assert.Equal((ServiceHarness.ZoneCode, ServiceLevel.Standard, 1), (row.ZoneCode, row.Level, row.Shipments));
        Assert.Equal(new Money(8.00m), row.Revenue);
    }

    [Fact]
    public async Task Money_order_report_shows_only_the_masked_document_number()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        harness.As(harness.Auditor);

        var page = await harness.Reports().MoneyOrdersAsync(TodayFor(null), new PageRequest(), Ct);

        var row = Assert.Single(page.Items);
        Assert.Equal(new Money(50.00m), row.Amount);
        Assert.Equal(new Money(2.00m), row.Fees);
        Assert.Equal("Passport ****5678 – Passed", row.Verification);
    }

    [Fact]
    public async Task Low_stock_report_lists_items_at_or_below_the_threshold()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);
        await harness.StockAdjustments().AdjustAsync(
            new StockAdjustmentCommand { Sku = ServiceHarness.ReducedSku, Change = -5, Reason = "Damaged" },
            Ct);

        var report = await harness.Reports().LowStockAsync(null, Ct);

        Assert.Equal(5, report.Threshold);
        Assert.Equal(ServiceHarness.ReducedSku, Assert.Single(report.Rows).Sku.Value);
    }

    [Fact]
    public async Task Clerk_cannot_open_the_reports()
    {
        using var harness = new ServiceHarness();

        await Assert.ThrowsAsync<ForbiddenException>(() => harness.Reports().DailySalesAsync(TodayFor(null), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => harness.Reports().LowStockAsync(null, Ct));
    }

    [Fact]
    public async Task Manager_sees_only_the_own_branch()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        harness.As(harness.OtherManager);

        Assert.Empty(await harness.Reports().DailySalesAsync(TodayFor(null), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            harness.Reports().DailySalesAsync(TodayFor(harness.Branch.Code), Ct));
    }

    [Fact]
    public async Task Auditor_may_filter_by_any_branch()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        harness.As(harness.Auditor);

        Assert.Single(await harness.Reports().DailySalesAsync(TodayFor(harness.Branch.Code), Ct));
        Assert.Empty(await harness.Reports().DailySalesAsync(TodayFor(harness.OtherBranch.Code), Ct));
    }

    [Fact]
    public async Task Dashboard_combines_todays_sales_low_stock_and_alerts()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.ExemptSku, 6);
        await checkout.PayAsync(id, PaymentMethod.Cash, Ct);

        var dashboard = await harness.Reports().DashboardAsync(null, Ct);

        Assert.Equal(harness.Branch.Code, dashboard.Branch);
        Assert.Equal(2, dashboard.Transactions);
        Assert.Equal(new Money(25.00m), dashboard.Retail);
        Assert.Single(dashboard.LowStock.Rows);
        Assert.Equal(NotificationKind.LowStock, Assert.Single(dashboard.Alerts).Kind);
    }

    private static ReportFilter TodayFor(BranchCode? branch) => new(branch, Today, Today);

    /// <summary>Retail 10.00 + parcel 8.00 + money order 50.00 with fee 2.00, verified, paid by card.</summary>
    private static async Task PaidMixedSaleAsync(ServiceHarness harness)
    {
        harness.As(harness.Clerk);
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku);
        await checkout.AddParcelAsync(id, 2m);
        await checkout.AddMoneyOrderAsync(id, 50m);
        await checkout.VerifyAsync(id);
        await checkout.PayAsync(id, PaymentMethod.Card, Ct);
    }
}
