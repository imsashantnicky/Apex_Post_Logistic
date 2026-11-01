using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Branches;
using Microsoft.Extensions.Options;

namespace ApexPost.Application.Reports;

/// <summary>
/// The four reports, the dashboard and the notification list. Clerks and managers only see their own branch;
/// auditors and administrators may pick any branch or all branches. Clerks get the dashboard, not the reports.
/// </summary>
public sealed class ReportService(
    IReportQueries queries,
    INotificationSink notifications,
    ICurrentUser user,
    IClock clock,
    IOptions<StockSettings> stockSettings)
{
    public const int DashboardAlertCount = 5;
    public const int NotificationPageSize = 50;
    public const int DefaultRangeDays = 14;

    private int LowStockThreshold => stockSettings.Value.LowStockThreshold;

    /// <summary>The filter of a report page; without dates it covers the last two weeks up to today.</summary>
    public ReportFilter FilterFor(BranchCode? branch, DateOnly? from, DateOnly? to)
    {
        var end = to ?? clock.Today;
        return new ReportFilter(branch, from ?? end.AddDays(-DefaultRangeDays), end);
    }

    public Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(
        ReportFilter filter,
        CancellationToken cancellationToken) =>
        queries.DailySalesAsync(Scoped(filter), cancellationToken);

    public async Task<LowStockReport> LowStockAsync(BranchCode? branch, CancellationToken cancellationToken)
    {
        RequireReportViewer();
        return await LowStockReportAsync(user.ScopeTo(branch), cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ShippingVolumeRow>> ShippingVolumeAsync(
        ReportFilter filter,
        CancellationToken cancellationToken) =>
        queries.ShippingVolumeAsync(Scoped(filter), cancellationToken);

    public Task<MoneyOrderReport> MoneyOrdersAsync(
        ReportFilter filter,
        PageRequest page,
        CancellationToken cancellationToken) =>
        queries.MoneyOrdersAsync(Scoped(filter), page, cancellationToken);

    /// <summary>Sales, stock and alerts do not depend on each other, so they are loaded in parallel.</summary>
    public async Task<Dashboard> DashboardAsync(BranchCode? branch, CancellationToken cancellationToken)
    {
        var scope = user.ScopeTo(branch);
        var today = clock.Today;
        var salesTask = queries.DailySalesAsync(new ReportFilter(scope, today, today), cancellationToken);
        var stockTask = LowStockReportAsync(scope, cancellationToken);
        var alertsTask = notifications.RecentAsync(scope, DashboardAlertCount, cancellationToken);
        await Task.WhenAll(salesTask, stockTask, alertsTask).ConfigureAwait(false);

        var sales = await salesTask.ConfigureAwait(false);
        return new Dashboard(
            scope,
            today,
            sales.Sum(row => row.Transactions),
            sales.Select(row => row.Retail).Total(),
            sales.Select(row => row.Services).Total(),
            sales.Select(row => row.Total).Total(),
            await stockTask.ConfigureAwait(false),
            await alertsTask.ConfigureAwait(false));
    }

    public Task<IReadOnlyList<Notification>> NotificationsAsync(
        BranchCode? branch,
        CancellationToken cancellationToken) =>
        notifications.RecentAsync(user.ScopeTo(branch), NotificationPageSize, cancellationToken);

    private async Task<LowStockReport> LowStockReportAsync(BranchCode? branch, CancellationToken cancellationToken)
    {
        var rows = await queries.LowStockAsync(branch, LowStockThreshold, cancellationToken).ConfigureAwait(false);
        return new LowStockReport(LowStockThreshold, rows);
    }

    private ReportFilter Scoped(ReportFilter filter)
    {
        RequireReportViewer();
        return filter with { Branch = user.ScopeTo(filter.Branch) };
    }

    private void RequireReportViewer()
    {
        if (!user.CanViewReports)
        {
            throw new ForbiddenException($"The {user.Role} role cannot read reports.");
        }
    }
}
