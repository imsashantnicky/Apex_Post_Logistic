using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Reports;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Configurations;
using ApexPost.Infrastructure.Persistence.Mappers;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories.Queries;

/// <summary>
/// The reports as set-based SQL: the database adds up the rows and only the totals travel. Each call opens its own
/// short-lived context, because the dashboard runs these queries in parallel and one context cannot do that. A cast
/// through object reads a converted column as its stored value, which is what SUM and DATE need.
/// </summary>
public sealed class SqlReportQueries(IDbContextFactory<ApexPostDbContext> contexts) : IReportQueries
{
    private const string TransactionKey = "TransactionId";

    public async Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            var paid = Paid(database, filter);
            var days = await paid
                .GroupBy(transaction => new
                {
                    Day = ((DateTime)(object)transaction.Payment!.PaidAt).Date,
                    transaction.Branch,
                })
                .Select(group => new
                {
                    group.Key.Day,
                    group.Key.Branch,
                    Count = group.Count(),
                    Tax = group.Sum(transaction => (decimal)(object)transaction.Payment!.Tax),
                })
                .OrderBy(day => day.Day)
                .ThenBy(day => day.Branch)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var retail = await PerDayAsync(RetailAmounts(database), paid, cancellationToken).ConfigureAwait(false);
            var services = await PerDayAsync(ServiceAmounts(database), paid, cancellationToken).ConfigureAwait(false);

            return
            [
                .. days.Select(day =>
                {
                    var key = (day.Day, day.Branch);
                    var sales = new Money(retail.GetValueOrDefault(key));
                    var other = new Money(services.GetValueOrDefault(key));
                    var tax = new Money(day.Tax);
                    return new DailySalesRow(
                        DateOnly.FromDateTime(day.Day),
                        day.Branch,
                        day.Count,
                        sales,
                        other,
                        tax,
                        sales + other + tax);
                }),
            ];
        }
    }

    public async Task<IReadOnlyList<LowStockRow>> LowStockAsync(
        BranchCode? branch,
        int threshold,
        CancellationToken cancellationToken)
    {
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            var low = database.Set<StockItem>().AsNoTracking().Where(item => item.Quantity <= threshold);
            if (branch is not null)
            {
                low = low.Where(item => item.Branch == branch);
            }

            return await low
                .Join(
                    database.Set<Product>(),
                    item => item.Sku,
                    product => product.Id,
                    (item, product) => new { item.Branch, item.Sku, product.Name, item.Quantity })
                .OrderBy(row => row.Quantity)
                .ThenBy(row => row.Branch)
                .ThenBy(row => row.Sku)
                .Select(row => new LowStockRow(row.Branch, row.Sku, row.Name, row.Quantity))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// SQL groups by the measures of a shipment and the domain turns each group into chargeable weight, so the rule
    /// exists once and still only a few rows travel.
    /// </summary>
    public async Task<IReadOnlyList<ShippingVolumeRow>> ShippingVolumeAsync(
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            var groups = await database.Set<ShippingLineItem>()
                .AsNoTracking()
                .Join(
                    Paid(database, filter),
                    line => EF.Property<Guid>(line, TransactionKey),
                    transaction => transaction.Id,
                    (line, _) => line)
                .GroupBy(line => new
                {
                    line.ZoneCode,
                    line.Shipment.Level,
                    line.Shipment.Weight,
                    line.Shipment.Dimensions.LengthCm,
                    line.Shipment.Dimensions.WidthCm,
                    line.Shipment.Dimensions.HeightCm,
                })
                .Select(group => new
                {
                    group.Key,
                    Count = group.Count(),
                    Revenue = group.Sum(line => (decimal)(object)line.Price),
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return
            [
                .. groups
                    .GroupBy(row => (row.Key.ZoneCode, row.Key.Level))
                    .OrderBy(group => group.Key.ZoneCode, StringComparer.Ordinal)
                    .ThenBy(group => group.Key.Level)
                    .Select(group => new ShippingVolumeRow(
                        group.Key.ZoneCode,
                        group.Key.Level,
                        group.Sum(row => row.Count),
                        group.Sum(row => row.Count * Shipment.ChargeableKgOf(
                            row.Key.Weight,
                            new Dimensions(row.Key.LengthCm, row.Key.WidthCm, row.Key.HeightCm))),
                        new Money(group.Sum(row => row.Revenue)))),
            ];
        }
    }

    public async Task<MoneyOrderReport> MoneyOrdersAsync(
        ReportFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(page);
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            var relevant = Relevant(database, filter);
            var totals = await BranchTotalsAsync(database, relevant, cancellationToken).ConfigureAwait(false);
            var count = await relevant.CountAsync(cancellationToken).ConfigureAwait(false);
            var rows = await MoneyOrderRowsAsync(database, relevant, page, cancellationToken).ConfigureAwait(false);
            return new MoneyOrderReport(totals, new PagedResult<MoneyOrderRow>(rows, page.Number, page.Size, count));
        }
    }

    /// <summary>Only money orders still paid count as issued; every identity check counts, whatever followed.</summary>
    private static async Task<List<MoneyOrderBranchRow>> BranchTotalsAsync(
        ApexPostDbContext database,
        IQueryable<CounterTransaction> relevant,
        CancellationToken cancellationToken)
    {
        var branches = await relevant
            .Select(transaction => transaction.Branch)
            .Distinct()
            .OrderBy(branch => branch)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var issued = await database.Set<MoneyOrderLineItem>()
            .AsNoTracking()
            .Join(
                relevant.Where(transaction => transaction.Status == TransactionStatus.Paid),
                order => EF.Property<Guid>(order, TransactionKey),
                transaction => transaction.Id,
                (order, transaction) => new { transaction.Branch, order })
            .GroupBy(row => row.Branch)
            .Select(group => new
            {
                Branch = group.Key,
                Count = group.Count(),
                Amount = group.Sum(row => (decimal)(object)row.order.Amount),
                Fees = group.Sum(row => (decimal)(object)row.order.Fee),
            })
            .ToDictionaryAsync(row => row.Branch, cancellationToken)
            .ConfigureAwait(false);
        var checks = await database.Set<IdentityVerificationRecord>()
            .AsNoTracking()
            .Join(
                relevant,
                check => check.TransactionId,
                transaction => transaction.Id,
                (check, transaction) => new { transaction.Branch, check.Result })
            .GroupBy(row => new { row.Branch, row.Result })
            .Select(group => new { group.Key.Branch, group.Key.Result, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int Checks(BranchCode branch, VerificationResult result) =>
            checks.Find(row => row.Branch == branch && row.Result == result)?.Count ?? 0;

        return
        [
            .. branches.Select(branch =>
            {
                var orders = issued.GetValueOrDefault(branch);
                return new MoneyOrderBranchRow(
                    branch,
                    orders?.Count ?? 0,
                    new Money(orders?.Amount ?? 0m),
                    new Money(orders?.Fees ?? 0m),
                    Checks(branch, VerificationResult.Passed),
                    Checks(branch, VerificationResult.Failed),
                    Checks(branch, VerificationResult.Pending));
            }),
        ];
    }

    /// <summary>One query for the page: the figures of each row are subqueries, not a query per row.</summary>
    private static async Task<List<MoneyOrderRow>> MoneyOrderRowsAsync(
        ApexPostDbContext database,
        IQueryable<CounterTransaction> relevant,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var orders = database.Set<MoneyOrderLineItem>();
        var checks = database.Set<IdentityVerificationRecord>();
        var rows = await relevant
            .OrderByDescending(transaction => transaction.OpenedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip(page.Skip)
            .Take(page.Size)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.Branch,
                transaction.OpenedAt,
                transaction.Status,
                Orders = orders.Count(order => EF.Property<Guid>(order, TransactionKey) == transaction.Id),
                Amount = orders
                    .Where(order => EF.Property<Guid>(order, TransactionKey) == transaction.Id)
                    .Sum(order => (decimal?)(object)order.Amount),
                Fees = orders
                    .Where(order => EF.Property<Guid>(order, TransactionKey) == transaction.Id)
                    .Sum(order => (decimal?)(object)order.Fee),
                Latest = checks
                    .Where(check => check.TransactionId == transaction.Id)
                    .OrderByDescending(check => check.VerifiedAt)
                    .ThenByDescending(check => EF.Property<DateTime>(check, ColumnRules.CreatedAt))
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new MoneyOrderRow(
                row.Id,
                row.Branch,
                row.OpenedAt,
                row.Status,
                row.Orders,
                new Money(row.Amount ?? 0m),
                new Money(row.Fees ?? 0m),
                row.Latest is null ? null : IdentityVerificationMapper.ToDomain(row.Latest).Summary)),
        ];
    }

    private static IQueryable<CounterTransaction> Paid(ApexPostDbContext database, ReportFilter filter)
    {
        var start = ReportDays.Start(filter.From);
        var end = ReportDays.EndOf(filter.To);
        var paid = database.Set<CounterTransaction>()
            .AsNoTracking()
            .Where(transaction => transaction.Status == TransactionStatus.Paid
                && transaction.Payment!.PaidAt >= start
                && transaction.Payment!.PaidAt < end);
        return filter.Branch is { } branch ? paid.Where(transaction => transaction.Branch == branch) : paid;
    }

    /// <summary>Transactions of the period with a money order or an identity check.</summary>
    private static IQueryable<CounterTransaction> Relevant(ApexPostDbContext database, ReportFilter filter)
    {
        var start = ReportDays.Start(filter.From);
        var end = ReportDays.EndOf(filter.To);
        var orders = database.Set<MoneyOrderLineItem>();
        var checks = database.Set<IdentityVerificationRecord>();
        var relevant = database.Set<CounterTransaction>()
            .AsNoTracking()
            .Where(transaction =>
                (transaction.Payment != null ? transaction.Payment.PaidAt : transaction.OpenedAt) >= start
                && (transaction.Payment != null ? transaction.Payment.PaidAt : transaction.OpenedAt) < end)
            .Where(transaction =>
                orders.Any(order => EF.Property<Guid>(order, TransactionKey) == transaction.Id)
                || checks.Any(check => check.TransactionId == transaction.Id));
        return filter.Branch is { } branch ? relevant.Where(transaction => transaction.Branch == branch) : relevant;
    }

    private static IQueryable<LineAmount> RetailAmounts(ApexPostDbContext database) =>
        database.Set<RetailLineItem>().AsNoTracking().Select(line => new LineAmount
        {
            TransactionId = EF.Property<Guid>(line, TransactionKey),
            Amount = (decimal)(object)line.UnitPrice * line.Quantity,
        });

    private static IQueryable<LineAmount> ServiceAmounts(ApexPostDbContext database) =>
        database.Set<ShippingLineItem>()
            .AsNoTracking()
            .Select(line => new LineAmount
            {
                TransactionId = EF.Property<Guid>(line, TransactionKey),
                Amount = (decimal)(object)line.Price,
            })
            .Concat(database.Set<MoneyOrderLineItem>().AsNoTracking().Select(order => new LineAmount
            {
                TransactionId = EF.Property<Guid>(order, TransactionKey),
                Amount = (decimal)(object)order.Amount + (decimal)(object)order.Fee,
            }));

    private static Task<Dictionary<(DateTime Day, BranchCode Branch), decimal>> PerDayAsync(
        IQueryable<LineAmount> amounts,
        IQueryable<CounterTransaction> paid,
        CancellationToken cancellationToken) =>
        amounts
            .Join(
                paid,
                line => line.TransactionId,
                transaction => transaction.Id,
                (line, transaction) => new
                {
                    Day = ((DateTime)(object)transaction.Payment!.PaidAt).Date,
                    transaction.Branch,
                    line.Amount,
                })
            .GroupBy(row => new { row.Day, row.Branch })
            .Select(group => new { group.Key.Day, group.Key.Branch, Amount = group.Sum(row => row.Amount) })
            .ToDictionaryAsync(row => (row.Day, row.Branch), row => row.Amount, cancellationToken);

    private sealed class LineAmount
    {
        public Guid TransactionId { get; init; }

        public decimal Amount { get; init; }
    }
}
