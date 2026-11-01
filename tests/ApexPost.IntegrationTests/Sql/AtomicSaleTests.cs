using System.Net;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>Stock, status and payment of a sale are one commit, and a failed commit leaves no trace.</summary>
public sealed class AtomicSaleTests(CommandLogFactory factory) : IClassFixture<CommandLogFactory>
{
    private const string FirstSku = "PKG-0009";
    private const string SecondSku = "PKG-0010";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_sale_is_one_commit_of_stock_transaction_and_payment()
    {
        const string sku = "STA-0001";
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var before = await factory.QuantityAsync(sku);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(sku, 2);
        var savesBefore = factory.Saves;

        using var paid = await counter.PayAsync();

        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        Assert.Equal(savesBefore + 1, factory.Saves);
        Assert.Equal(before - 2, await factory.QuantityAsync(sku));
        var stored = await StoredTransaction.ReadAsync(factory, counter.Id);
        Assert.Equal(TransactionStatus.Paid, stored.Status);
        Assert.NotNull(stored.Payment);
    }

    [Fact]
    public async Task A_conflict_on_the_last_stock_row_undoes_the_whole_sale()
    {
        await factory.SetQuantityAsync(FirstSku, 10);
        await factory.SetQuantityAsync(SecondSku, 10);
        using var server = InterruptedStock.Server(factory, () => factory.SetQuantityAsync(SecondSku, 7));
        using var clerk = await CounterBrowser.SignInAsync(server, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var first = await counter.AddRetailAsync(FirstSku);
        using var second = await counter.AddRetailAsync(SecondSku);

        using var paid = await counter.PayAsync();

        Assert.Equal(10, await factory.QuantityAsync(FirstSku));
        Assert.Equal(7, await factory.QuantityAsync(SecondSku));
        var stored = await StoredTransaction.ReadAsync(factory, counter.Id);
        Assert.Equal(TransactionStatus.Open, stored.Status);
        Assert.Null(stored.Payment);
        var audit = await AuditTrail.EntryForAsync(factory, paid, AuditEvents.Payment);
        Assert.Equal(StatusCodes.Conflict, audit.StatusCode);
    }

    [Fact]
    public async Task A_rolled_back_sale_can_be_paid_again()
    {
        const string sku = "STA-0002";
        await factory.SetQuantityAsync(sku, 5);
        var firstTry = true;
        using var server = InterruptedStock.Server(factory, async () =>
        {
            if (firstTry)
            {
                firstTry = false;
                await factory.SetQuantityAsync(sku, 4);
            }
        });
        using var clerk = await CounterBrowser.SignInAsync(server, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(sku);

        using var refused = await counter.PayAsync();
        using var accepted = await counter.PayAsync();

        Assert.Equal(TransactionStatus.Paid, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
        Assert.Equal(3, await factory.QuantityAsync(sku));
    }

    [Fact]
    public async Task A_line_cannot_slip_into_a_basket_that_was_paid_meanwhile()
    {
        const string sku = "STA-0003";
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(sku);
        await using var stale = factory.OpenDatabase();
        var key = Guid.Parse(counter.Id);
        var transaction = await stale.Set<CounterTransaction>().SingleAsync(item => item.Id == key, Ct);
        using var paid = await counter.PayAsync();

        stale.Entry(transaction).Property("ModifiedAtUtc").CurrentValue = DateTime.UtcNow;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task A_failed_save_after_a_set_based_update_rolls_the_update_back()
    {
        const decimal original = 123.45m;
        var sku = new Sku("OFF-0001");
        await SetPriceAsync(sku, original);
        await using var scope = factory.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var staff = scope.ServiceProvider.GetRequiredService<IStaffRepository>();
        var existing = await factory.StaffAsync(DemoAccounts.Clerk);

        await Assert.ThrowsAsync<DuplicateKeyException>(() => unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await products.RevisePricesAsync(null, 10m, DateTimeOffset.UtcNow, token);
                staff.Add(new Staff(
                    Guid.NewGuid(),
                    "Copy",
                    existing.Email,
                    StaffRole.CounterClerk,
                    existing.HomeBranch));
            },
            Ct));

        Assert.Equal(new Money(original), await PriceOfAsync(sku));
    }

    [Fact]
    public async Task Work_that_throws_inside_a_transaction_leaves_the_prices_alone()
    {
        const decimal original = 234.56m;
        var sku = new Sku("OFF-0002");
        await SetPriceAsync(sku, original);
        await using var scope = factory.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await products.RevisePricesAsync(null, 10m, DateTimeOffset.UtcNow, token);
                throw new InvalidOperationException("The rest of the use case failed.");
            },
            Ct));

        Assert.Equal(new Money(original), await PriceOfAsync(sku));
    }

    private async Task SetPriceAsync(Sku sku, decimal price)
    {
        var money = new Money(price);
        await using var database = factory.OpenDatabase();
        await database.Set<Product>()
            .Where(product => product.Id == sku)
            .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.UnitPrice, money), Ct);
    }

    private async Task<Money> PriceOfAsync(Sku sku)
    {
        await using var database = factory.OpenDatabase();
        return await database.Set<Product>()
            .AsNoTracking()
            .Where(product => product.Id == sku)
            .Select(product => product.UnitPrice)
            .SingleAsync(Ct);
    }

    private static class StatusCodes
    {
        public const int Conflict = 409;
    }
}
