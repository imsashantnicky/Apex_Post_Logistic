using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Reports;

/// <summary>
/// Builds the reports in memory from the repositories, filtered with specifications. A database version
/// replaces this class with SQL queries; the report service does not change.
/// </summary>
public sealed class RepositoryReportQueries(
    IRepository<CounterTransaction, Guid> transactions,
    IRepository<StockItem, StockKey> stock,
    IProductCatalog products) : IReportQueries
{
    public async Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        var paid = await transactions.ListAsync(ReportSpecifications.PaidTransactions(filter), cancellationToken)
            .ConfigureAwait(false);
        return
        [
            .. paid
                .GroupBy(transaction => (Day: PaidDay(transaction), transaction.Branch))
                .OrderBy(group => group.Key.Day)
                .ThenBy(group => group.Key.Branch.Value, StringComparer.Ordinal)
                .Select(group => new DailySalesRow(
                    group.Key.Day,
                    group.Key.Branch,
                    group.Count(),
                    group.Select(transaction => transaction.RetailTotal).Total(),
                    group.Select(transaction => transaction.ServicesTotal).Total(),
                    group.Select(transaction => transaction.Tax).Total(),
                    group.Select(transaction => transaction.Total).Total())),
        ];
    }

    public async Task<IReadOnlyList<LowStockRow>> LowStockAsync(
        BranchCode? branch,
        int threshold,
        CancellationToken cancellationToken)
    {
        var specification = ReportSpecifications.LowStock(branch, LowStockRule.AtOrBelow(threshold));
        var low = await stock.ListAsync(specification, cancellationToken).ConfigureAwait(false);
        var names = (await products.ListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(product => product.Sku, product => product.Name);
        return
        [
            .. low
                .OrderBy(item => item.Quantity)
                .ThenBy(item => item.Branch.Value, StringComparer.Ordinal)
                .ThenBy(item => item.Sku.Value, StringComparer.Ordinal)
                .Select(item => new LowStockRow(
                    item.Branch,
                    item.Sku,
                    names.GetValueOrDefault(item.Sku, item.Sku.Value),
                    item.Quantity)),
        ];
    }

    public async Task<IReadOnlyList<ShippingVolumeRow>> ShippingVolumeAsync(
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        var paid = await transactions.ListAsync(ReportSpecifications.PaidTransactions(filter), cancellationToken)
            .ConfigureAwait(false);
        return
        [
            .. paid
                .SelectMany(transaction => transaction.Lines.OfType<ShippingLineItem>())
                .GroupBy(line => (line.ZoneCode, line.Shipment.Level))
                .OrderBy(group => group.Key.ZoneCode, StringComparer.Ordinal)
                .ThenBy(group => group.Key.Level)
                .Select(group => new ShippingVolumeRow(
                    group.Key.ZoneCode,
                    group.Key.Level,
                    group.Count(),
                    group.Sum(line => line.Shipment.ChargeableKg),
                    group.Select(line => line.Total).Total())),
        ];
    }

    public async Task<PagedResult<MoneyOrderRow>> MoneyOrdersAsync(
        ReportFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var matching = await transactions.ListAsync(ReportSpecifications.Transactions(filter), cancellationToken)
            .ConfigureAwait(false);
        MoneyOrderRow[] rows =
        [
            .. matching
                .Where(transaction => transaction.Verifications.Count > 0
                    || transaction.Lines.Any(line => line is MoneyOrderLineItem))
                .OrderByDescending(transaction => transaction.OpenedAt)
                .Select(ToMoneyOrderRow),
        ];
        return rows.ToPage(page);
    }

    private static MoneyOrderRow ToMoneyOrderRow(CounterTransaction transaction)
    {
        var orders = transaction.Lines.OfType<MoneyOrderLineItem>().ToList();
        return new(
            transaction.Id,
            transaction.Branch,
            transaction.OpenedAt,
            transaction.Status,
            orders.Count,
            orders.Select(order => order.Amount).Total(),
            orders.Select(order => order.Fee).Total(),
            transaction.VerificationSummary);
    }

    private static DateOnly PaidDay(CounterTransaction transaction) =>
        DateOnly.FromDateTime((transaction.Payment?.PaidAt ?? transaction.OpenedAt).UtcDateTime);
}
