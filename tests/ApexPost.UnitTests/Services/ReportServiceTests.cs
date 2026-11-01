using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Reports;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Options;
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

        var report = await harness.Reports().MoneyOrdersAsync(TodayFor(null), new PageRequest(), Ct);

        var row = Assert.Single(report.Transactions.Items);
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

    [Fact]
    public async Task Date_range_includes_both_ends_and_nothing_outside()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        harness.As(harness.Auditor);
        var reports = harness.Reports();

        Assert.Single(await reports.DailySalesAsync(new ReportFilter(null, Today, Today.AddDays(3)), Ct));
        Assert.Single(await reports.DailySalesAsync(new ReportFilter(null, Today.AddDays(-3), Today), Ct));
        Assert.Empty(await reports.DailySalesAsync(new ReportFilter(null, Today.AddDays(1), Today.AddDays(3)), Ct));
        var earlier = new ReportFilter(null, Today.AddDays(-3), Today.AddDays(-1));
        Assert.Empty(await reports.ShippingVolumeAsync(earlier, Ct));
        Assert.Empty((await reports.MoneyOrdersAsync(earlier, new PageRequest(), Ct)).Transactions.Items);
    }

    [Fact]
    public async Task Filter_defaults_to_the_last_two_weeks_and_never_runs_backwards()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Auditor);
        var reports = harness.Reports();

        var byDefault = reports.FilterFor(null, null, null);
        var backwards = reports.FilterFor(null, Today, Today.AddDays(-5));

        Assert.Equal((Today.AddDays(-14), Today), (byDefault.From, byDefault.To));
        Assert.Equal((Today, Today), (backwards.From, backwards.To));
    }

    [Fact]
    public async Task Sales_are_one_row_per_day_and_branch()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        harness.Clock.UtcNow = TestData.Now.AddDays(1);
        await PaidMixedSaleAsync(harness);
        await PaidParcelAtOtherBranchAsync(harness);
        harness.As(harness.Auditor);

        var rows = await harness.Reports().DailySalesAsync(new ReportFilter(null, Today, Today.AddDays(1)), Ct);

        Assert.Equal(
            [(Today, "BR001"), (Today.AddDays(1), "BR001"), (Today.AddDays(1), "BR002")],
            rows.Select(row => (row.Day, row.Branch.Value)));
    }

    [Fact]
    public async Task Voided_and_refunded_sales_leave_sales_and_shipping_volume()
    {
        using var harness = new ServiceHarness();
        var voided = await PaidMixedSaleAsync(harness);
        var refunded = await PaidMixedSaleAsync(harness);
        await PaidMixedSaleAsync(harness);
        harness.As(harness.Manager);
        await harness.Reversal().VoidAsync(voided, new ReversalCommand { Reason = "Mistake" }, Ct);
        await harness.Reversal().RefundAsync(refunded, new ReversalCommand { Reason = "Damaged" }, Ct);

        var sales = Assert.Single(await harness.Reports().DailySalesAsync(TodayFor(null), Ct));
        var shipping = Assert.Single(await harness.Reports().ShippingVolumeAsync(TodayFor(null), Ct));

        Assert.Equal(1, sales.Transactions);
        Assert.Equal(1, shipping.Shipments);
    }

    [Fact]
    public async Task Money_order_totals_count_issued_orders_and_every_check_per_branch()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        var voided = await PaidMixedSaleAsync(harness);
        harness.As(harness.Clerk);
        harness.Verifier.Result = VerificationResult.Failed;
        var failed = await harness.OpenAsync();
        await harness.Checkout().VerifyAsync(failed);
        harness.Verifier.Result = VerificationResult.Pending;
        await harness.Checkout().VerifyAsync(failed);
        harness.As(harness.Manager);
        await harness.Reversal().VoidAsync(voided, new ReversalCommand { Reason = "Mistake" }, Ct);
        harness.As(harness.Auditor);

        var report = await harness.Reports().MoneyOrdersAsync(TodayFor(null), new PageRequest(), Ct);

        var branch = Assert.Single(report.Branches);
        Assert.Equal(harness.Branch.Code, branch.Branch);
        Assert.Equal(1, branch.MoneyOrders);
        Assert.Equal(new Money(50.00m), branch.Amount);
        Assert.Equal(new Money(2.00m), branch.Fees);
        Assert.Equal((2, 1, 1), (branch.ChecksPassed, branch.ChecksFailed, branch.ChecksPending));
        Assert.Equal(3, report.Transactions.TotalCount);
    }

    [Fact]
    public async Task Money_order_page_is_cut_but_the_branch_totals_are_not()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        await PaidMixedSaleAsync(harness);
        await PaidMixedSaleAsync(harness);
        harness.As(harness.Manager);

        var report = await harness.Reports().MoneyOrdersAsync(TodayFor(null), new PageRequest(2, 2), Ct);

        Assert.Single(report.Transactions.Items);
        Assert.Equal(3, report.Transactions.TotalCount);
        Assert.Equal(3, Assert.Single(report.Branches).MoneyOrders);
    }

    [Fact]
    public async Task Manager_cannot_read_another_branch_in_any_report()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.OtherManager);
        var reports = harness.Reports();
        var foreign = harness.Branch.Code;

        await Assert.ThrowsAsync<ForbiddenException>(() => reports.DailySalesAsync(TodayFor(foreign), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => reports.LowStockAsync(foreign, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => reports.ShippingVolumeAsync(TodayFor(foreign), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            reports.MoneyOrdersAsync(TodayFor(foreign), new PageRequest(), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => reports.DashboardAsync(foreign, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => reports.NotificationsAsync(foreign, Ct));
    }

    [Fact]
    public async Task Manager_report_without_a_branch_is_limited_to_the_own_branch()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        await PaidParcelAtOtherBranchAsync(harness);
        harness.As(harness.OtherManager);
        var reports = harness.Reports();

        var sales = Assert.Single(await reports.DailySalesAsync(TodayFor(null), Ct));
        var shipping = Assert.Single(await reports.ShippingVolumeAsync(TodayFor(null), Ct));

        Assert.Equal(harness.OtherBranch.Code, sales.Branch);
        Assert.Equal(1, shipping.Shipments);
        Assert.Empty((await reports.MoneyOrdersAsync(TodayFor(null), new PageRequest(), Ct)).Branches);
    }

    [Fact]
    public async Task Auditor_without_a_branch_reads_all_branches()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        await PaidParcelAtOtherBranchAsync(harness);
        harness.As(harness.Auditor);

        Assert.Equal(2, (await harness.Reports().DailySalesAsync(TodayFor(null), Ct)).Count);
    }

    [Fact]
    public async Task Low_stock_lists_the_lowest_first_and_only_the_chosen_branch()
    {
        using var harness = new ServiceHarness();
        harness.Stock.Seed(TestData.CreateStock(ServiceHarness.StandardSku, 1, TestData.OtherBranch));
        harness.Stock.Seed(TestData.CreateStock(ServiceHarness.ReducedSku, 0, TestData.OtherBranch));
        harness.Stock.Seed(TestData.CreateStock(ServiceHarness.ExemptSku, 6, TestData.OtherBranch));
        harness.As(harness.Auditor);

        var chosen = await harness.Reports().LowStockAsync(harness.OtherBranch.Code, Ct);
        var home = await harness.Reports().LowStockAsync(harness.Branch.Code, Ct);

        Assert.Equal([ServiceHarness.ReducedSku, ServiceHarness.StandardSku], chosen.Rows.Select(row => row.Sku.Value));
        Assert.Empty(home.Rows);
    }

    [Fact]
    public async Task Notifications_stay_within_the_branch_unless_the_user_sees_all()
    {
        using var harness = new ServiceHarness();
        await PaidMixedSaleAsync(harness);
        var voided = await PaidMixedSaleAsync(harness);
        harness.As(harness.Manager);
        await harness.Reversal().VoidAsync(voided, new ReversalCommand { Reason = "Mistake" }, Ct);

        Assert.Single(await harness.Reports().NotificationsAsync(null, Ct));
        harness.As(harness.OtherManager);
        Assert.Empty(await harness.Reports().NotificationsAsync(null, Ct));
        harness.As(harness.Auditor);
        Assert.Single(await harness.Reports().NotificationsAsync(null, Ct));
    }

    [Fact]
    public async Task Dashboard_loads_sales_and_stock_at_the_same_time()
    {
        using var harness = new ServiceHarness();
        var queries = new RendezvousQueries();
        var reports = new ReportService(
            queries,
            harness.Sink,
            harness.User,
            harness.Clock,
            Options.Create(harness.StockSettings));

        var dashboard = await reports.DashboardAsync(null, Ct).WaitAsync(RendezvousQueries.Patience, Ct);

        Assert.Equal(0, dashboard.Transactions);
        Assert.Empty(dashboard.LowStock.Rows);
    }

    private static ReportFilter TodayFor(BranchCode? branch) => new(branch, Today, Today);

    /// <summary>Retail 10.00 + parcel 8.00 + money order 50.00 with fee 2.00, verified, paid by card.</summary>
    private static async Task<Guid> PaidMixedSaleAsync(ServiceHarness harness)
    {
        harness.As(harness.Clerk);
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku);
        await checkout.AddParcelAsync(id, 2m);
        await checkout.AddMoneyOrderAsync(id, 50m);
        await checkout.VerifyAsync(id);
        await checkout.PayAsync(id, PaymentMethod.Card, Ct);
        return id;
    }

    private static async Task PaidParcelAtOtherBranchAsync(ServiceHarness harness)
    {
        harness.As(harness.OtherClerk);
        var checkout = harness.Checkout();
        var id = await checkout.OpenAsync(harness.OtherBranch.Counters[0].Id, Ct);
        await checkout.AddParcelAsync(id, 2m);
        await checkout.PayAsync(id, PaymentMethod.Card, Ct);
    }

    /// <summary>Neither query returns until the other has started, so running them in turn never finishes.</summary>
    private sealed class RendezvousQueries : IReportQueries
    {
        public static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

        private readonly TaskCompletionSource _salesStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _stockStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(
            ReportFilter filter,
            CancellationToken cancellationToken)
        {
            _salesStarted.SetResult();
            await _stockStarted.Task.WaitAsync(cancellationToken);
            return [];
        }

        public async Task<IReadOnlyList<LowStockRow>> LowStockAsync(
            BranchCode? branch,
            int threshold,
            CancellationToken cancellationToken)
        {
            _stockStarted.SetResult();
            await _salesStarted.Task.WaitAsync(cancellationToken);
            return [];
        }

        public Task<IReadOnlyList<ShippingVolumeRow>> ShippingVolumeAsync(
            ReportFilter filter,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MoneyOrderReport> MoneyOrdersAsync(
            ReportFilter filter,
            PageRequest page,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
