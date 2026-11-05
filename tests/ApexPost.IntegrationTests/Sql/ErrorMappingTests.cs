using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Application.Abstractions;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Repositories;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>How database and rule failures reach a caller: 409, 404 and 400 as Problem Details.</summary>
public sealed class ErrorMappingTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string ProblemJson = "application/problem+json";
    private const string StaffForm = "/Staff/Create";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_stock_conflict_is_a_409_problem_for_a_json_caller()
    {
        const string sku = "OFF-0003";
        using var server = InterruptedStock.Server(factory, () => factory.SetQuantityAsync(sku, 3));
        using var clerk = await CounterBrowser.SignInAsync(server, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(sku);
        clerk.AcceptJson();

        using var paid = await counter.PayAsync();

        var problem = await AssertProblemAsync(paid, HttpStatusCode.Conflict);
        Assert.Contains("changed by someone else", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_duplicate_key_the_precheck_missed_is_a_409_problem()
    {
        using var server = WithBlindEmailCheck();
        using var admin = await SignInAsAdminAsync(server);
        admin.AcceptJson();

        using var created = await PostStaffAsync(admin, DemoAccounts.Clerk);

        await AssertProblemAsync(created, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_duplicate_key_on_a_form_returns_to_the_form_and_is_audited_as_409()
    {
        using var server = WithBlindEmailCheck();
        using var admin = await SignInAsAdminAsync(server);

        using var created = await PostStaffAsync(admin, DemoAccounts.Manager);

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Contains(
            "already exists",
            await admin.ReadPageAsync(created.Headers.Location!.OriginalString),
            StringComparison.Ordinal);
        Assert.Equal(409, (await AuditTrail.EntryForAsync(factory, created, AuditEvents.StaffCreated)).StatusCode);
    }

    [Fact]
    public async Task A_transaction_that_does_not_exist_is_404()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var missing = await clerk.GetAsync($"/Transactions/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task A_broken_business_rule_is_a_400_problem_with_the_rule_message()
    {
        const string sku = "OFF-0004";
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var line = await counter.AddRetailAsync(sku, await factory.QuantityAsync(sku) + 1);
        clerk.AcceptJson();

        using var paid = await counter.PayAsync();

        var problem = await AssertProblemAsync(paid, HttpStatusCode.BadRequest);
        Assert.Contains("Not enough stock", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stale_staff_form_is_refused_by_the_version_column()
    {
        const string email = "clerk3.br002@apexpost.test";
        using var admin = await SignInAsAdminAsync(factory);
        var member = await factory.StaffAsync(email);
        var editPath = $"/Staff/Edit/{member.Id}";
        using var first = await admin.PostAsync(
            editPath,
            $"/Staff/Deactivate/{member.Id}",
            new Dictionary<string, string> { ["version"] = Text(member.Version), ["reason"] = "Left the company." });

        using var stale = await admin.PostAsync(
            editPath,
            $"/Staff/Activate/{member.Id}",
            new Dictionary<string, string> { ["version"] = Text(member.Version) });

        Assert.Equal(409, (await AuditTrail.EntryForAsync(factory, stale, AuditEvents.StaffActivated)).StatusCode);
        Assert.False((await factory.StaffAsync(email)).IsActive);
    }

    [Fact]
    public async Task A_counter_with_history_stays_and_the_administrator_is_told_why()
    {
        const string code = "BR004";
        using var admin = await SignInAsAdminAsync(factory);
        var branch = await factory.BranchAsync(code);
        var detailsPath = $"/Branches/Details/{code}";

        using var removed = await admin.PostAsync(
            detailsPath,
            $"/Branches/RemoveCounter/{code}",
            new Dictionary<string, string>
            {
                ["counterId"] = branch.Counters[0].Id.ToString(),
                ["version"] = Text(branch.Version),
            });

        Assert.Equal(409, (await AuditTrail.EntryForAsync(factory, removed, AuditEvents.CounterRemoved)).StatusCode);
        Assert.Equal(branch.Counters.Count, (await factory.BranchAsync(code)).Counters.Count);
        Assert.Contains("has history", await admin.ReadPageAsync(detailsPath), StringComparison.Ordinal);
    }

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static Task<CounterBrowser> SignInAsAdminAsync(WebApplicationFactory<Program> server) =>
        AdminBrowser.SignInAsync(server, DemoAccounts.Administrator);

    private static Task<HttpResponseMessage> PostStaffAsync(CounterBrowser admin, string email) =>
        admin.PostAsync(StaffForm, StaffForm, new Dictionary<string, string>
        {
            ["Name"] = "Sam Taylor",
            ["Email"] = email,
            ["Role"] = nameof(StaffRole.CounterClerk),
            ["HomeBranch"] = "BR001",
            ["Password"] = "A-long-enough-password",
        });

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("correlationId").GetString()));
        return problem;
    }

    /// <summary>The pre-check of the service sees nothing, as when two admins save the same email at once.</summary>
    private WebApplicationFactory<Program> WithBlindEmailCheck() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<StaffRepository>();
            services.AddScoped<IStaffRepository>(provider =>
                new BlindStaff(provider.GetRequiredService<StaffRepository>()));
        }));

    private sealed class BlindStaff(IStaffRepository inner) : IStaffRepository
    {
        public Task<Staff?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Staff?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
            inner.FindByEmailAsync(email, cancellationToken);

        public Task<bool> EmailTakenAsync(string email, Guid? ownId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Add(Staff member) => inner.Add(member);

        public void Update(Staff member, int expectedVersion) => inner.Update(member, expectedVersion);
    }
}
