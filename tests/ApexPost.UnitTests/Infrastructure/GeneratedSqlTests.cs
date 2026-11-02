using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Repositories.Queries;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.UnitTests.Infrastructure;

/// <summary>
/// Keeps the SQL that the measured indexes depend on, without a server: ToQueryString shows what EF Core would send.
/// </summary>
public sealed class GeneratedSqlTests
{
    private static readonly DateOnly Day = new(2026, 11, 1);
    private static readonly ReportFilter Filter = new(new BranchCode("BR001"), Day, Day);

    [Fact]
    public void The_money_order_period_stays_two_ranges_an_index_can_seek()
    {
        using var database = OfflineDatabase();

        var sql = SqlReportQueries.Relevant(database, Filter).ToQueryString();

        Assert.DoesNotContain("CASE", sql, StringComparison.Ordinal);
        Assert.Contains("[p].[PaidAt] >= @start", sql, StringComparison.Ordinal);
        Assert.Contains("[c0].[OpenedAt] >= @start0", sql, StringComparison.Ordinal);
        Assert.Contains("UNION ALL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void The_transaction_page_sorts_in_the_order_of_its_index()
    {
        using var database = OfflineDatabase();
        var matching = SqlTransactionQueries.Matching(database, new TransactionFilter(Filter.Branch));

        var sql = SqlTransactionQueries.PageOf(matching, new PageRequest()).ToQueryString();

        Assert.Contains("ORDER BY [c].[OpenedAt] DESC, [c].[Id] DESC", sql, StringComparison.Ordinal);
        Assert.Contains("OFFSET", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Shipping_volume_filters_on_the_payment_date_itself()
    {
        using var database = OfflineDatabase();

        var sql = SqlReportQueries.ShippingGroups(database, Filter).ToQueryString();

        Assert.Contains("[p].[PaidAt] >= @start", sql, StringComparison.Ordinal);
        Assert.Contains("[p].[PaidAt] < @end", sql, StringComparison.Ordinal);
    }

    private static ApexPostDbContext OfflineDatabase() =>
        new(new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer("Server=unused;Database=unused").Options);
}
