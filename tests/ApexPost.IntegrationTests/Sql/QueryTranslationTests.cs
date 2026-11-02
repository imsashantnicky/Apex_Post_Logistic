using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Transactions;
using ApexPost.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>
/// LINQ query services run on SQL Server and are compared with hand-written SQL over the seeded history. A query EF Core
/// cannot translate fails here at run time, which an in-memory fake would never show.
/// </summary>
public sealed class QueryTranslationTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Branch = "BR001";
    private const string Paid = "Status = 'Paid'";

    private static readonly DateOnly From = new(2000, 1, 1);
    private static readonly DateOnly To = new(2100, 1, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static BranchCode Code => new(Branch);

    [Fact]
    public async Task Transaction_list_counts_filters_and_pages_like_sql()
    {
        var expected = await factory.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CounterTransactions WHERE BranchCode = '{Branch}' AND {Paid}");
        var filter = new TransactionFilter(Code, Status: TransactionStatus.Paid);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITransactionQueries>();

        var first = await queries.ListAsync(filter, new PageRequest(1, 5), Ct);
        var second = await queries.ListAsync(filter, new PageRequest(2, 5), Ct);

        Assert.True(expected > 10, "The seeded history should fill two pages.");
        Assert.Equal(expected, first.TotalCount);
        Assert.Equal(5, first.Items.Count);
        Assert.Empty(first.Items.Select(row => row.Id).Intersect(second.Items.Select(row => row.Id)));
        Assert.All(first.Items, row => Assert.Equal(TransactionStatus.Paid, row.Status));
        Assert.All(first.Items, row => Assert.Equal(Code, row.Branch));
        var opened = first.Items.Concat(second.Items).Select(row => row.OpenedAt).ToList();
        Assert.Equal(opened.OrderDescending(), opened);
    }

    [Fact]
    public async Task Transaction_list_includes_both_ends_of_the_date_range()
    {
        var day = await factory.ScalarAsync<DateTime>(
            "SELECT TOP (1) CAST(OpenedAt AS date) FROM CounterTransactions ORDER BY OpenedAt");
        var expected = await factory.ScalarAsync<int>($"""
            SELECT COUNT(*) FROM CounterTransactions
            WHERE CAST(OpenedAt AS date) = '{day:yyyy-MM-dd}'
            """);
        var only = DateOnly.FromDateTime(day);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITransactionQueries>();

        var rows = await queries.ListAsync(new TransactionFilter(From: only, To: only), new PageRequest(), Ct);

        Assert.Equal(expected, rows.TotalCount);
    }

    [Fact]
    public async Task List_totals_equal_the_aggregate_of_the_same_transaction()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITransactionQueries>();
        var filter = new TransactionFilter(Code, Status: TransactionStatus.Paid);

        var rows = await queries.ListAsync(filter, new PageRequest(1, 10), Ct);

        foreach (var row in rows.Items)
        {
            var transaction = await queries.FindAsync(row.Id, Ct);
            Assert.NotNull(transaction);
            Assert.Equal(transaction.Total, row.Total);
            Assert.Equal(transaction.Lines.Count, row.LineCount);
        }
    }

    [Fact]
    public async Task An_unknown_transaction_is_null_and_not_an_error()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITransactionQueries>();

        Assert.Null(await queries.FindAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Shipping_volume_adds_up_to_the_paid_shipments()
    {
        var shipments = await factory.ScalarAsync<int>($"""
            SELECT COUNT(*) FROM Shipments s JOIN CounterTransactions c ON c.Id = s.TransactionId WHERE c.{Paid}
            """);
        var revenue = await factory.ScalarAsync<decimal>($"""
            SELECT SUM(s.Price) FROM Shipments s JOIN CounterTransactions c ON c.Id = s.TransactionId WHERE c.{Paid}
            """);
        await using var scope = factory.Services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportQueries>();

        var rows = await reports.ShippingVolumeAsync(new ReportFilter(null, From, To), Ct);

        Assert.True(shipments > 0);
        Assert.Equal(shipments, rows.Sum(row => row.Shipments));
        Assert.Equal(revenue, rows.Sum(row => row.Revenue.Amount));
    }

    [Fact]
    public async Task Money_order_totals_count_only_what_is_still_paid()
    {
        var count = await factory.ScalarAsync<int>($"""
            SELECT COUNT(*) FROM MoneyOrders m JOIN CounterTransactions c ON c.Id = m.TransactionId WHERE c.{Paid}
            """);
        var amount = await factory.ScalarAsync<decimal>($"""
            SELECT SUM(m.Amount) FROM MoneyOrders m JOIN CounterTransactions c ON c.Id = m.TransactionId WHERE c.{Paid}
            """);
        var checks = await factory.ScalarAsync<int>("SELECT COUNT(*) FROM IdentityVerifications");
        await using var scope = factory.Services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportQueries>();

        var report = await reports.MoneyOrdersAsync(new ReportFilter(null, From, To), new PageRequest(1, 5), Ct);

        Assert.Equal(count, report.Branches.Sum(row => row.MoneyOrders));
        Assert.Equal(amount, report.Branches.Sum(row => row.Amount.Amount));
        Assert.Equal(
            checks,
            report.Branches.Sum(row => row.ChecksPassed + row.ChecksFailed + row.ChecksPending));
        Assert.All(
            report.Transactions.Items.Where(row => row.Verification is not null),
            row => Assert.Contains("****", row.Verification, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Low_stock_through_the_port_equals_the_table()
    {
        var expected = await factory.ScalarAsync<int>("SELECT COUNT(*) FROM BranchStock WHERE Quantity <= 25");
        await using var scope = factory.Services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportQueries>();

        var rows = await reports.LowStockAsync(null, 25, Ct);

        Assert.True(expected > 0);
        Assert.Equal(expected, rows.Count);
    }

    [Fact]
    public async Task Catalogue_search_matches_name_and_sku_and_treats_wildcards_as_text()
    {
        var byName = await factory.ScalarAsync<int>("SELECT COUNT(*) FROM Products WHERE Name LIKE '%en%'");
        await using var scope = factory.Services.CreateAsyncScope();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueQueries>();

        var named = await catalogue.SearchAsync(Code, "en", null, Ct);
        var bySku = await catalogue.SearchAsync(Code, "PKG-0001", null, Ct);
        var wildcard = await catalogue.SearchAsync(Code, "%", null, Ct);

        Assert.True(named.Count >= byName);
        Assert.Equal("PKG-0001", Assert.Single(bySku).Sku.ToString());
        Assert.Empty(wildcard);
    }

    [Fact]
    public async Task Catalogue_quantities_and_categories_come_from_the_selected_branch()
    {
        var expected = await factory.ScalarAsync<int>($"""
            SELECT COUNT(*) FROM Products p
            JOIN BranchStock s ON s.Sku = p.Sku AND s.BranchCode = '{Branch}'
            WHERE p.Category = 'Stationery'
            """);
        var quantity = await factory.ScalarAsync<int>(
            $"SELECT Quantity FROM BranchStock WHERE BranchCode = '{Branch}' AND Sku = 'STA-0001'");
        await using var scope = factory.Services.CreateAsyncScope();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueQueries>();

        var items = await catalogue.SearchAsync(Code, string.Empty, ProductCategory.Stationery, Ct);

        Assert.Equal(expected, items.Count);
        Assert.Equal(quantity, items.Single(item => item.Sku.ToString() == "STA-0001").InStock);
    }

    [Fact]
    public async Task Branch_and_staff_lists_return_every_row_with_their_counters()
    {
        var branches = await factory.ScalarAsync<int>("SELECT COUNT(*) FROM Branches");
        var staff = await factory.ScalarAsync<int>("SELECT COUNT(*) FROM Staff");
        var counters = await factory.ScalarAsync<int>("SELECT COUNT(*) FROM Counters");
        await using var scope = factory.Services.CreateAsyncScope();

        var branchRows = await scope.ServiceProvider.GetRequiredService<IBranchQueries>().ListAsync(Ct);
        var staffRows = await scope.ServiceProvider.GetRequiredService<IStaffQueries>().ListAsync(Ct);

        Assert.Equal(branches, branchRows.Count);
        Assert.Equal(staff, staffRows.Count);
        Assert.Equal(counters, branchRows.Sum(branch => branch.Counters.Count));
    }

    [Fact]
    public async Task A_cancelled_token_stops_each_kind_of_database_call()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var token = cancelled.Token;
        var filter = new ReportFilter(null, From, To);
        await using var scope = factory.Services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportQueries>();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionQueries>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reports.DailySalesAsync(filter, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reports.ShippingVolumeAsync(filter, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            transactions.ListAsync(new TransactionFilter(), new PageRequest(), token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unitOfWork.SaveChangesAsync(token));
    }
}
