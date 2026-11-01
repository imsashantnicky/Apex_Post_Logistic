using System.Net;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Repositories;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;
using Microsoft.EntityFrameworkCore;
using BranchCode = ApexPost.Domain.Branches.BranchCode;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>
/// The stock gate only covers one server, so these races run on two: each host has its own gate and only the
/// RowVersion of the stock row stands between the clerks and an oversold item.
/// </summary>
public sealed class StockConcurrencyTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Branch = "BR001";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_contexts_reading_the_last_unit_cannot_both_take_it()
    {
        const string sku = "PKG-0004";
        await factory.SetQuantityAsync(sku, 1);
        await using var first = factory.OpenDatabase();
        await using var second = factory.OpenDatabase();
        var firstItem = (await ReadAsync(first, sku)).Single();
        var secondItem = (await ReadAsync(second, sku)).Single();

        firstItem.Remove(1);
        new StockRepository(first).Update([firstItem]);
        await new UnitOfWork(first).SaveChangesAsync(Ct);
        secondItem.Remove(1);
        new StockRepository(second).Update([secondItem]);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(second).SaveChangesAsync(Ct));
        Assert.Equal(0, await factory.QuantityAsync(sku));
    }

    [Fact]
    public async Task Two_servers_selling_the_last_unit_at_once_sell_it_once_and_the_loser_is_told_409()
    {
        const string sku = "PKG-0005";
        await factory.SetQuantityAsync(sku, 1);
        var bothRead = new Rendezvous(2);
        using var server1 = InterruptedStock.Server(factory, bothRead.ArriveAsync);
        using var server2 = InterruptedStock.Server(factory, bothRead.ArriveAsync);
        using var clerk1 = await CounterBrowser.SignInAsync(server1, DemoAccounts.Clerk);
        using var clerk2 = await CounterBrowser.SignInAsync(server2, DemoAccounts.SecondClerk);
        var first = await Counter.OpenAsync(clerk1);
        var second = await Counter.OpenAsync(clerk2);
        using var firstLine = await first.AddRetailAsync(sku);
        using var secondLine = await second.AddRetailAsync(sku);

        var responses = await Task.WhenAll(first.PayAsync(), second.PayAsync());

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));
        var audited = await Task.WhenAll(responses.Select(response =>
            AuditTrail.EntryForAsync(factory, response, AuditEvents.Payment)));
        Assert.Equal([302, 409], audited.Select(entry => entry.StatusCode).Order());
        Assert.Equal(0, await factory.QuantityAsync(sku));
        var stored = await Task.WhenAll(
            StoredTransaction.ReadAsync(factory, first.Id),
            StoredTransaction.ReadAsync(factory, second.Id));
        Assert.Single(stored, transaction => transaction.Status == TransactionStatus.Paid);
        Assert.Single(stored, transaction => transaction is { Status: TransactionStatus.Open, Payment: null });
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task A_form_post_that_loses_the_race_shows_the_conflict_message()
    {
        const string sku = "PKG-0006";
        using var server = InterruptedStock.Server(factory, () => factory.SetQuantityAsync(sku, 1));
        using var clerk = await CounterBrowser.SignInAsync(server, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(sku);

        using var paid = await counter.PayAsync();

        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        Assert.Contains("changed by someone else", await counter.PageAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_basket_paid_on_two_servers_takes_the_stock_once()
    {
        const string sku = "PKG-0007";
        await factory.SetQuantityAsync(sku, 10);
        var bothRead = new Rendezvous(2);
        using var server1 = InterruptedStock.Server(factory, bothRead.ArriveAsync);
        using var server2 = InterruptedStock.Server(factory, bothRead.ArriveAsync);
        using var clerk1 = await CounterBrowser.SignInAsync(server1, DemoAccounts.Clerk);
        using var clerk2 = await CounterBrowser.SignInAsync(server2, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk1);
        using var line = await counter.AddRetailAsync(sku, 2);

        var responses = await Task.WhenAll(counter.PayAsync(), counter.For(clerk2).PayAsync());

        Assert.Equal(8, await factory.QuantityAsync(sku));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task The_database_itself_refuses_negative_stock()
    {
        var branch = new BranchCode(Branch);
        var sku = new Sku("PKG-0008");
        await using var database = factory.OpenDatabase();

        await Assert.ThrowsAnyAsync<Exception>(() => database.Set<StockItem>()
            .Where(item => item.Branch == branch && item.Sku == sku)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Quantity, -1), Ct));
    }

    private static Task<List<StockItem>> ReadAsync(
        ApexPostDbContext database,
        string sku)
    {
        var branch = new BranchCode(Branch);
        var product = new Sku(sku);
        return database.Set<StockItem>()
            .Where(item => item.Branch == branch && item.Sku == product)
            .ToListAsync(Ct);
    }
}
