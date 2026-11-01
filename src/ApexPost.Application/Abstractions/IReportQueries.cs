using ApexPost.Application.DTOs;
using ApexPost.Application.Reports;
using ApexPost.Domain.Branches;

namespace ApexPost.Application.Abstractions;

/// <summary>The data behind the four reports, filtered by branch (null = all) and date range.</summary>
public interface IReportQueries
{
    Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(ReportFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<LowStockRow>> LowStockAsync(
        BranchCode? branch,
        int threshold,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ShippingVolumeRow>> ShippingVolumeAsync(
        ReportFilter filter,
        CancellationToken cancellationToken);

    Task<PagedResult<MoneyOrderRow>> MoneyOrdersAsync(
        ReportFilter filter,
        PageRequest page,
        CancellationToken cancellationToken);
}
