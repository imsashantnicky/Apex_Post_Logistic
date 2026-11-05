using System.Globalization;
using System.Net;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using ApexPost.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>Fields a form does not offer never reach a row, however they are added to the post.</summary>
public sealed class OverPostingTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Attacker = "attacker";
    private const string ForgedDate = "2000-01-01";
    private const string CreatedBy = "CreatedBy";
    private const string CreatedAtUtc = "CreatedAtUtc";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_counter_sale_ignores_status_id_and_audit_columns_in_the_post()
    {
        var started = DateTime.UtcNow;
        var forgedId = Guid.NewGuid();
        var forged = Forged(forgedId);
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var staffId = (await factory.StaffAsync(DemoAccounts.Clerk)).Id;
        var branch = await factory.BranchAsync("BR001");
        var counterId = branch.Counters[0].Id.ToString();

        using var opened = await clerk.PostAsync(
            "/Transactions/New",
            "/Transactions/Create",
            new Dictionary<string, string>(forged) { ["counterId"] = counterId });
        var counter = new Counter(clerk, opened.Headers.Location!.OriginalString);
        using var line = await PostStepAsync(clerk, counter, "AddRetail", new(forged)
        {
            ["Sku"] = "STA-0004",
            ["Quantity"] = "1",
        });
        using var paid = await PostStepAsync(clerk, counter, "Pay", new(forged) { ["method"] = "Card" });

        Assert.Equal(HttpStatusCode.Redirect, line.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        Assert.Equal(counter.Path, paid.Headers.Location?.OriginalString);
        var key = Guid.Parse(counter.Id);
        await using var database = factory.OpenDatabase();
        var row = await database.Set<CounterTransaction>()
            .AsNoTracking()
            .Where(transaction => transaction.Id == key)
            .Select(transaction => new
            {
                transaction.Status,
                By = EF.Property<string>(transaction, CreatedBy),
                At = EF.Property<DateTime>(transaction, CreatedAtUtc),
            })
            .SingleAsync(Ct);
        var forgedRows = await database.Set<CounterTransaction>().AsNoTracking()
            .CountAsync(transaction => transaction.Id == forgedId, Ct);
        var storedLines = await database.Set<TransactionLine>().AsNoTracking()
            .CountAsync(stored => EF.Property<Guid>(stored, "TransactionId") == key, Ct);

        Assert.NotEqual(forgedId, key);
        Assert.Equal(0, forgedRows);
        Assert.Equal(TransactionStatus.Paid, row.Status);
        Assert.Equal(1, storedLines);
        Assert.Equal(staffId.ToString(), row.By, ignoreCase: true);
        Assert.InRange(row.At, started.AddMinutes(-1), DateTime.UtcNow);
    }

    [Fact]
    public async Task A_staff_edit_ignores_id_active_flag_and_audit_columns_in_the_post()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        var target = await factory.StaffAsync("clerk3.br003@apexpost.test");
        var bystander = await factory.StaffAsync("clerk1.br004@apexpost.test");
        var columnsBefore = await AuditColumnsAsync(target.Id);

        using var saved = await admin.PostAsync(
            "/Staff",
            $"/Staff/Edit/{target.Id}",
            new Dictionary<string, string>(Forged(bystander.Id))
            {
                ["IsActive"] = "false",
                ["version"] = target.Version.ToString(CultureInfo.InvariantCulture),
                ["Name"] = "Edited Name",
                ["Email"] = target.Email,
                ["Role"] = target.Role.ToString(),
                ["HomeBranch"] = target.HomeBranch.Value,
            });

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var stored = await factory.StaffAsync(target.Email);
        Assert.Equal(target.Id, stored.Id);
        Assert.Equal("Edited Name", stored.Name);
        Assert.True(stored.IsActive);
        Assert.Equal(columnsBefore, await AuditColumnsAsync(target.Id));
        Assert.Equal(bystander.Name, (await factory.StaffAsync(bystander.Email)).Name);
    }

    private static Dictionary<string, string> Forged(Guid id) => new()
    {
        ["Status"] = nameof(TransactionStatus.Voided),
        ["Id"] = id.ToString(),
        [CreatedBy] = Attacker,
        [CreatedAtUtc] = ForgedDate,
    };

    private static Task<HttpResponseMessage> PostStepAsync(
        CounterBrowser browser,
        Counter counter,
        string action,
        Dictionary<string, string> fields) =>
        browser.PostAsync(counter.Path, $"{Counter.TransactionsPath}/{action}/{counter.Id}", fields);

    private async Task<(string By, DateTime At)> AuditColumnsAsync(Guid staffId)
    {
        await using var database = factory.OpenDatabase();
        var columns = await database.Set<Staff>()
            .AsNoTracking()
            .Where(member => member.Id == staffId)
            .Select(member => new
            {
                By = EF.Property<string>(member, CreatedBy),
                At = EF.Property<DateTime>(member, CreatedAtUtc),
            })
            .SingleAsync(Ct);
        return (columns.By, columns.At);
    }
}
