using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Application.Reports;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Configurations;
using ApexPost.Infrastructure.Persistence.Mappers;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.Data.SqlClient;
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
    private const int DailySalesUnknownBranch = 51003;
    private const int LowStockUnknownBranch = 51012;

    /// <summary>One procedure call instead of three queries: ADR-012 explains the trade.</summary>
    public async Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var branch = ProcedureParameters.OptionalBranch(filter.Branch);
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            try
            {
                var rows = await database.Database
                    .SqlQuery<DailySalesProcedureRow>(
                        $"""
                        EXEC dbo.usp_DailyBranchSalesRange
                            @From = {filter.From}, @To = {filter.To}, @BranchCode = {branch}
                        """)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                return [.. rows.Select(ToDailySales)];
            }
            catch (SqlException exception) when (exception.Number == DailySalesUnknownBranch)
            {
                throw new NotFoundException(exception.Message);
            }
        }
    }

    public async Task<IReadOnlyList<LowStockRow>> LowStockAsync(
        BranchCode? branch,
        int threshold,
        CancellationToken cancellationToken)
    {
        var code = ProcedureParameters.OptionalBranch(branch);
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            try
            {
                var rows = await database.Database
                    .SqlQuery<LowStockProcedureRow>(
                        $"EXEC dbo.usp_LowStockReport @Threshold = {threshold}, @BranchCode = {code}")
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                return
                [
                    .. rows.Select(row => new LowStockRow(
                        new BranchCode(row.BranchCode),
                        new Sku(row.Sku),
                        row.Product,
                        row.Quantity)),
                ];
            }
            catch (SqlException exception) when (exception.Number == LowStockUnknownBranch)
            {
                throw new NotFoundException(exception.Message);
            }
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
            var groups = await ShippingGroups(database, filter).ToListAsync(cancellationToken).ConfigureAwait(false);

            return
            [
                .. groups
                    .GroupBy(row => (row.ZoneCode, row.Level))
                    .OrderBy(group => group.Key.ZoneCode, StringComparer.Ordinal)
                    .ThenBy(group => group.Key.Level)
                    .Select(group => new ShippingVolumeRow(
                        group.Key.ZoneCode,
                        group.Key.Level,
                        group.Sum(row => row.Count),
                        group.Sum(row => row.Count * Shipment.ChargeableKgOf(
                            row.Weight,
                            new Dimensions(row.LengthCm, row.WidthCm, row.HeightCm))),
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
        IQueryable<PeriodTransaction> relevant,
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
        IQueryable<PeriodTransaction> relevant,
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

    internal static IQueryable<ShippingGroup> ShippingGroups(ApexPostDbContext database, ReportFilter filter) =>
        database.Set<ShippingLineItem>()
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
            .Select(group => new ShippingGroup
            {
                ZoneCode = group.Key.ZoneCode,
                Level = group.Key.Level,
                Weight = group.Key.Weight,
                LengthCm = group.Key.LengthCm,
                WidthCm = group.Key.WidthCm,
                HeightCm = group.Key.HeightCm,
                Count = group.Count(),
                Revenue = group.Sum(line => (decimal)(object)line.Price),
            });

    private static DailySalesRow ToDailySales(DailySalesProcedureRow row)
    {
        var retail = new Money(row.Retail);
        var services = new Money(row.Services);
        var tax = new Money(row.Tax);
        return new DailySalesRow(
            row.SalesDay,
            new BranchCode(row.BranchCode),
            row.Transactions,
            retail,
            services,
            tax,
            retail + services + tax);
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

    /// <summary>
    /// Transactions of the period with a money order or an identity check. A paid transaction counts by its payment
    /// date, any other by its opening date. Those are two ranges on two indexed columns, so each is a query of its own:
    /// one CASE over both columns would hide them from the index (PERFORMANCE.md, non-SARGable predicate).
    /// </summary>
    internal static IQueryable<PeriodTransaction> Relevant(ApexPostDbContext database, ReportFilter filter)
    {
        var start = ReportDays.Start(filter.From);
        var end = ReportDays.EndOf(filter.To);
        var orders = database.Set<MoneyOrderLineItem>();
        var checks = database.Set<IdentityVerificationRecord>();
        var transactions = database.Set<CounterTransaction>().AsNoTracking();
        if (filter.Branch is { } branch)
        {
            transactions = transactions.Where(transaction => transaction.Branch == branch);
        }

        return Heads(transactions.Where(transaction =>
                transaction.Payment != null
                && transaction.Payment.PaidAt >= start
                && transaction.Payment.PaidAt < end))
            .Concat(Heads(transactions.Where(transaction =>
                transaction.Payment == null
                && transaction.OpenedAt >= start
                && transaction.OpenedAt < end)))
            .Where(transaction =>
                orders.Any(order => EF.Property<Guid>(order, TransactionKey) == transaction.Id)
                || checks.Any(check => check.TransactionId == transaction.Id));
    }

    private static IQueryable<PeriodTransaction> Heads(IQueryable<CounterTransaction> transactions) =>
        transactions.Select(transaction => new PeriodTransaction
        {
            Id = transaction.Id,
            Branch = transaction.Branch,
            OpenedAt = transaction.OpenedAt,
            Status = transaction.Status,
        });

    /// <summary>The columns of a transaction that the money order report reads.</summary>
    internal sealed class PeriodTransaction
    {
        public Guid Id { get; init; }

        public BranchCode Branch { get; init; } = null!;

        public DateTimeOffset OpenedAt { get; init; }

        public TransactionStatus Status { get; init; }
    }

    /// <summary>The shipments of one set of measures in one zone and level.</summary>
    internal sealed class ShippingGroup
    {
        public string ZoneCode { get; init; } = string.Empty;

        public ServiceLevel Level { get; init; }

        public Weight Weight { get; init; } = null!;

        public decimal LengthCm { get; init; }

        public decimal WidthCm { get; init; }

        public decimal HeightCm { get; init; }

        public int Count { get; init; }

        public decimal Revenue { get; init; }
    }
}
