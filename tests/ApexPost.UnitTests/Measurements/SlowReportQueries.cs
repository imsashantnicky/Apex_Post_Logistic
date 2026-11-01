using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Reports;
using ApexPost.Domain.Branches;

namespace ApexPost.UnitTests.Measurements;

/// <summary>Adds the latency a database round trip would have to the two queries of the dashboard.</summary>
internal sealed class SlowReportQueries(IReportQueries inner, TimeSpan latency) : IReportQueries
{
    public async Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        await Task.Delay(latency, cancellationToken);
        return await inner.DailySalesAsync(filter, cancellationToken);
    }

    public async Task<IReadOnlyList<LowStockRow>> LowStockAsync(
        BranchCode? branch,
        int threshold,
        CancellationToken cancellationToken)
    {
        await Task.Delay(latency, cancellationToken);
        return await inner.LowStockAsync(branch, threshold, cancellationToken);
    }

    public Task<IReadOnlyList<ShippingVolumeRow>> ShippingVolumeAsync(
        ReportFilter filter,
        CancellationToken cancellationToken) =>
        inner.ShippingVolumeAsync(filter, cancellationToken);

    public Task<MoneyOrderReport> MoneyOrdersAsync(
        ReportFilter filter,
        PageRequest page,
        CancellationToken cancellationToken) =>
        inner.MoneyOrdersAsync(filter, page, cancellationToken);
}
