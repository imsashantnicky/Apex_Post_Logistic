using System.Net;
using ApexPost.Application.Abstractions;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Repositories.Sql;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>The audit log has its own transaction: it follows the business commit and can never undo it.</summary>
public sealed class AuditPersistenceTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Sku = "PKG-0012";
    private const string DocumentNumber = "AB9876543";

    [Fact]
    public async Task A_sale_writes_who_where_what_and_the_outcome_to_the_audit_table()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(Sku);

        using var paid = await counter.PayAsync();

        var entry = await AuditTrail.EntryForAsync(factory, paid, AuditEvents.Payment);
        Assert.Equal((await factory.StaffAsync(DemoAccounts.Clerk)).Id, entry.StaffId);
        Assert.Equal(StaffRole.CounterClerk, entry.Role);
        Assert.Equal("BR001", entry.Branch?.Value);
        Assert.Equal("Transactions", entry.Controller);
        Assert.Equal("Pay", entry.Action);
        Assert.Equal(302, entry.StatusCode);
        Assert.Equal(counter.Id, entry.RouteValues["id"]);
    }

    [Fact]
    public async Task A_refused_sale_stays_unpaid_but_is_still_audited()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        var available = await factory.QuantityAsync(Sku);
        using var line = await counter.AddRetailAsync(Sku, available + 1);

        using var paid = await counter.PayAsync();

        Assert.Equal(TransactionStatus.Open, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
        Assert.Equal(available, await factory.QuantityAsync(Sku));
        Assert.Equal(400, (await AuditTrail.EntryForAsync(factory, paid, AuditEvents.Payment)).StatusCode);
    }

    [Fact]
    public async Task The_audit_table_never_holds_a_document_number()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var verified = await counter.VerifyAsync(DocumentNumber);

        await AuditTrail.EntryForAsync(factory, verified, AuditEvents.IdentityVerification);
        Assert.DoesNotContain(DocumentNumber, await AuditTrail.RawRowsAsync(factory), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_audit_write_leaves_the_sale_committed_and_is_logged()
    {
        using var broken = WithDeadAuditLog();
        using var clerk = await CounterBrowser.SignInAsync(broken, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(Sku);
        var before = await factory.QuantityAsync(Sku);

        using var paid = await counter.PayAsync();

        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        Assert.Equal(TransactionStatus.Paid, (await StoredTransaction.ReadAsync(factory, counter.Id)).Status);
        Assert.Equal(before - 1, await factory.QuantityAsync(Sku));
        var correlationId = Assert.Single(paid.Headers.GetValues(ApexHeaders.CorrelationId));
        var entry = await factory.Logs.WaitForAsync(log => log.Level == LogLevel.Error
            && log.Message.Contains(correlationId, StringComparison.Ordinal));
        Assert.Contains(AuditEvents.Payment, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            await AuditTrail.EntriesAsync(factory),
            audit => audit.CorrelationId == correlationId);
    }

    /// <summary>A second server whose audit log points at a server that does not exist.</summary>
    private WebApplicationFactory<Program> WithDeadAuditLog() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IAuditStore>(provider => new SqlAuditStore(
                new NowhereContexts(),
                provider.GetRequiredService<ILogger<SqlAuditStore>>()))));

    private sealed class NowhereContexts : IDbContextFactory<ApexPostDbContext>
    {
        private const string Nowhere = "Server=127.0.0.1,1;Database=None;Connect Timeout=1";

        public ApexPostDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(Nowhere).Options);
    }
}
