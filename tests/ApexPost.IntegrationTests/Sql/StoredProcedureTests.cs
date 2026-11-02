using System.Data;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Records;
using ApexPost.Infrastructure.Repositories;
using ApexPost.Infrastructure.Repositories.Queries;
using ApexPost.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>The stored procedures on a real server: their results, their errors and their transaction.</summary>
public sealed class StoredProcedureTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Branch = "BR001";
    private const string Reason = "Stock count.";
    private const int UnknownBranchInReport = 51003;
    private const int RangeReversed = 51002;
    private const int ThresholdInvalid = 51011;
    private const int AdjustmentInvalid = 51021;
    private const int ForeignKeyViolation = 547;

    private static readonly DateOnly From = new(2000, 1, 1);
    private static readonly DateOnly To = new(2100, 1, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    [Fact]
    public async Task Daily_sales_equals_a_sum_worked_out_independently()
    {
        await using var database = factory.OpenDatabase();

        var report = await DailySalesAsync(database, null);
        var expected = await database.Database.SqlQuery<DailySalesProcedureRow>(
            $"""
            SELECT CAST(payment.PaidAt AS date) AS SalesDay, tx.BranchCode, COUNT(*) AS Transactions,
                   SUM(COALESCE(retail.Amount, 0)) AS Retail,
                   SUM(COALESCE(shipping.Amount, 0) + COALESCE(moneyOrder.Amount, 0)) AS Services,
                   SUM(payment.Tax) AS Tax
            FROM dbo.Payments AS payment
            JOIN dbo.CounterTransactions AS tx ON tx.Id = payment.TransactionId
            OUTER APPLY (SELECT SUM(UnitPrice * Quantity) AS Amount FROM dbo.TransactionLines
                         WHERE TransactionId = tx.Id) AS retail
            OUTER APPLY (SELECT SUM(Price) AS Amount FROM dbo.Shipments WHERE TransactionId = tx.Id) AS shipping
            OUTER APPLY (SELECT SUM(Amount + Fee) AS Amount FROM dbo.MoneyOrders
                         WHERE TransactionId = tx.Id) AS moneyOrder
            WHERE tx.Status = 'Paid'
            GROUP BY CAST(payment.PaidAt AS date), tx.BranchCode
            ORDER BY SalesDay, tx.BranchCode
            """).ToListAsync(Ct);

        Assert.NotEmpty(report);
        Assert.Equal(expected.Count, report.Count);
        Assert.All(expected.Zip(report), pair =>
        {
            Assert.Equal(pair.First.SalesDay, pair.Second.SalesDay);
            Assert.Equal(pair.First.BranchCode, pair.Second.BranchCode);
            Assert.Equal(pair.First.Transactions, pair.Second.Transactions);
            Assert.Equal(pair.First.Retail, pair.Second.Retail);
            Assert.Equal(pair.First.Services, pair.Second.Services);
            Assert.Equal(pair.First.Tax, pair.Second.Tax);
        });
    }

    [Fact]
    public async Task Daily_sales_of_one_branch_holds_only_that_branch()
    {
        await using var database = factory.OpenDatabase();

        var report = await DailySalesAsync(database, Branch);

        Assert.NotEmpty(report);
        Assert.All(report, row => Assert.Equal(Branch, row.BranchCode));
    }

    [Fact]
    public async Task Daily_sales_refuses_a_reversed_range_and_an_unknown_branch()
    {
        await using var database = factory.OpenDatabase();

        var reversed = await Assert.ThrowsAsync<SqlException>(() => database.Database
            .SqlQuery<DailySalesProcedureRow>($"EXEC dbo.usp_DailyBranchSales @From = {To}, @To = {From}")
            .ToListAsync(Ct));
        var unknown = await Assert.ThrowsAsync<SqlException>(() => DailySalesAsync(database, "BR999"));

        Assert.Equal(RangeReversed, reversed.Number);
        Assert.Equal(UnknownBranchInReport, unknown.Number);
    }

    [Fact]
    public async Task Low_stock_of_one_branch_is_its_share_of_the_all_branch_report()
    {
        await using var database = factory.OpenDatabase();
        const int threshold = 1000;

        var all = await LowStockAsync(database, threshold, null);
        var one = await LowStockAsync(database, threshold, Branch);

        Assert.NotEmpty(one);
        Assert.Equal(
            all.Where(row => row.BranchCode == Branch).Select(row => row.Sku),
            one.Select(row => row.Sku));
        Assert.Equal(one.OrderBy(row => row.Quantity).Select(row => row.Quantity), one.Select(row => row.Quantity));
    }

    [Fact]
    public async Task Low_stock_refuses_a_negative_threshold()
    {
        await using var database = factory.OpenDatabase();

        var refused = await Assert.ThrowsAsync<SqlException>(() => LowStockAsync(database, -1, null));

        Assert.Equal(ThresholdInvalid, refused.Number);
    }

    [Fact]
    public async Task An_adjustment_changes_the_quantity_and_writes_its_history()
    {
        const string sku = "STA-0001";
        await factory.SetQuantityAsync(sku, 10);
        var manager = await factory.StaffAsync(DemoAccounts.Manager);
        await using var database = factory.OpenDatabase();

        var quantity = await AdjustAsync(database, sku, -4, manager.Id);

        var history = await HistoryAsync(database, sku);
        Assert.Equal(6, quantity);
        Assert.Equal(6, await factory.QuantityAsync(sku));
        var row = Assert.Single(history);
        Assert.Equal(-4, row.Change);
        Assert.Equal(6, row.NewQuantity);
        Assert.Equal(Reason, row.Reason);
        Assert.Equal(manager.Id, row.StaffId);
    }

    [Fact]
    public async Task Too_large_a_reduction_changes_nothing_and_names_what_is_available()
    {
        const string sku = "STA-0002";
        await factory.SetQuantityAsync(sku, 2);
        var manager = await factory.StaffAsync(DemoAccounts.Manager);
        await using var database = factory.OpenDatabase();

        var refused = await Assert.ThrowsAsync<InsufficientStockException>(() =>
            AdjustAsync(database, sku, -3, manager.Id));

        Assert.Contains("3 requested, 2 available", refused.Message, StringComparison.Ordinal);
        Assert.Equal(2, await factory.QuantityAsync(sku));
        Assert.Empty(await HistoryAsync(database, sku));
    }

    [Fact]
    public async Task The_first_delivery_creates_the_stock_row()
    {
        const string sku = "STA-0003";
        var manager = await factory.StaffAsync(DemoAccounts.Manager);
        await using var database = factory.OpenDatabase();
        var branch = new BranchCode(Branch);
        var product = new Sku(sku);
        await database.Set<StockItem>()
            .Where(item => item.Branch == branch && item.Sku == product)
            .ExecuteDeleteAsync(Ct);

        var quantity = await AdjustAsync(database, sku, 7, manager.Id);

        Assert.Equal(7, quantity);
        Assert.Equal(7, await factory.QuantityAsync(sku));
    }

    [Fact]
    public async Task Unknown_products_are_not_found()
    {
        var manager = await factory.StaffAsync(DemoAccounts.Manager);
        await using var database = factory.OpenDatabase();

        await Assert.ThrowsAsync<NotFoundException>(() => AdjustAsync(database, "PKG-9999", 1, manager.Id));
    }

    [Fact]
    public async Task A_failure_after_the_update_rolls_the_quantity_back()
    {
        const string sku = "STA-0004";
        await factory.SetQuantityAsync(sku, 10);
        await using var database = factory.OpenDatabase();

        var refused = await Assert.ThrowsAsync<SqlException>(() => AdjustAsync(database, sku, 5, Guid.NewGuid()));

        Assert.Equal(ForeignKeyViolation, refused.Number);
        Assert.Equal(10, await factory.QuantityAsync(sku));
        Assert.Empty(await HistoryAsync(database, sku));
    }

    [Fact]
    public async Task A_blank_reason_is_refused_by_the_procedure_itself()
    {
        var manager = await factory.StaffAsync(DemoAccounts.Manager);
        await using var database = factory.OpenDatabase();

        var refused = await Assert.ThrowsAsync<SqlException>(() => new SqlStockAdjuster(database).AdjustAsync(
            new BranchCode(Branch),
            new Sku("STA-0005"),
            1,
            "   ",
            manager.Id,
            Now,
            Ct));

        Assert.Equal(AdjustmentInvalid, refused.Number);
    }

    [Fact]
    public async Task Parallel_adjustments_lose_no_update_and_each_keeps_its_own_history()
    {
        const string sku = "STA-0006";
        const int adjustments = 12;
        await factory.SetQuantityAsync(sku, 100);
        var manager = await factory.StaffAsync(DemoAccounts.Manager);

        await Task.WhenAll(Enumerable.Range(0, adjustments).Select(async _ =>
        {
            await using var database = factory.OpenDatabase();
            await AdjustAsync(database, sku, 1, manager.Id);
        }));

        await using var check = factory.OpenDatabase();
        var history = await HistoryAsync(check, sku);
        Assert.Equal(100 + adjustments, await factory.QuantityAsync(sku));
        Assert.Equal(
            Enumerable.Range(101, adjustments),
            history.Select(row => row.NewQuantity).Order());
    }

    private static Task<List<DailySalesProcedureRow>> DailySalesAsync(ApexPostDbContext database, string? branch)
    {
        var code = BranchParameter(branch);
        return database.Database
            .SqlQuery<DailySalesProcedureRow>(
                $"EXEC dbo.usp_DailyBranchSales @From = {From}, @To = {To}, @BranchCode = {code}")
            .ToListAsync(Ct);
    }

    private static Task<List<LowStockProcedureRow>> LowStockAsync(
        ApexPostDbContext database,
        int threshold,
        string? branch)
    {
        var code = BranchParameter(branch);
        return database.Database
            .SqlQuery<LowStockProcedureRow>(
                $"EXEC dbo.usp_LowStockReport @Threshold = {threshold}, @BranchCode = {code}")
            .ToListAsync(Ct);
    }

    private static SqlParameter BranchParameter(string? branch) =>
        new("@BranchCode", SqlDbType.VarChar, 5) { Value = (object?)branch ?? DBNull.Value };

    private static Task<int> AdjustAsync(ApexPostDbContext database, string sku, int change, Guid staffId) =>
        new SqlStockAdjuster(database).AdjustAsync(
            new BranchCode(Branch),
            new Sku(sku),
            change,
            Reason,
            staffId,
            Now,
            Ct);

    private static Task<List<StockAdjustmentRecord>> HistoryAsync(ApexPostDbContext database, string sku)
    {
        var product = new Sku(sku);
        return database.Set<StockAdjustmentRecord>()
            .AsNoTracking()
            .Where(record => record.Sku == product)
            .ToListAsync(Ct);
    }
}
