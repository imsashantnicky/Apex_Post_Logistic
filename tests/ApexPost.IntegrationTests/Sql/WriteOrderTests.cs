using ApexPost.Infrastructure.Persistence;
using ApexPost.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>
/// Reads the writes a sale really sends. Every commit takes its rows in one fixed order, stock rows by key first, so
/// two sales or a sale and a void never lock the same rows in opposite order (PERFORMANCE.md, deadlock avoidance).
/// </summary>
public sealed class WriteOrderTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string LaterSku = "STA-0002";
    private const string EarlierSku = "PKG-0003";

    [Fact]
    public async Task A_sale_writes_header_lines_stock_and_payment_in_that_order()
    {
        var recorder = new WriteRecorder();
        using var server = Recorded(recorder);
        using var clerk = await CounterBrowser.SignInAsync(server, DemoAccounts.Clerk);
        recorder.TakeWrites();

        var counter = await Counter.OpenAsync(clerk);
        var opened = recorder.TakeWrites();
        using var line = await counter.AddRetailAsync(LaterSku);
        var added = recorder.TakeWrites();
        using var otherLine = await counter.AddRetailAsync(EarlierSku);
        recorder.TakeWrites();
        using var paid = await counter.PayAsync();

        Assert.Equal(["INSERT CounterTransactions"], opened);
        Assert.Equal(["UPDATE CounterTransactions", "INSERT TransactionLines"], added);
        Assert.Equal(
            [
                $"UPDATE BranchStock {EarlierSku}", $"UPDATE BranchStock {LaterSku}",
                "UPDATE CounterTransactions", "INSERT Payments",
            ],
            recorder.TakeWrites());
    }

    [Fact]
    public async Task A_void_returns_stock_in_key_order_before_it_changes_the_header()
    {
        var recorder = new WriteRecorder();
        using var server = Recorded(recorder);
        using var clerk = await CounterBrowser.SignInAsync(server, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(server, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(LaterSku);
        using var otherLine = await counter.AddRetailAsync(EarlierSku);
        using var paid = await counter.PayAsync();
        recorder.TakeWrites();

        using var voided = await counter.For(manager).VoidAsync("Customer changed their mind.");

        Assert.Equal(
            [$"UPDATE BranchStock {EarlierSku}", $"UPDATE BranchStock {LaterSku}", "UPDATE CounterTransactions"],
            recorder.TakeWrites());
    }

    private WebApplicationFactory<Program> Recorded(WriteRecorder recorder) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<ApexPostDbContext>(options => options.AddInterceptors(recorder))));
}
