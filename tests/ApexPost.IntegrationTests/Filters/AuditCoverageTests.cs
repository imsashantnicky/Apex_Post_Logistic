using System.Net;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Personnel;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Filters;

namespace ApexPost.IntegrationTests.Filters;

public sealed class AuditCoverageTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string LoginPath = "/Account/Login";
    private const string Sku = "PKG-0001";

    [Fact]
    public async Task Sale_is_audited_step_by_step_with_who_where_and_correlation_id()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var opened = await Counter.PostOpenAsync(clerk);
        var counter = new Counter(clerk, opened.Headers.Location!.OriginalString);

        using var retail = await counter.AddRetailAsync(Sku);
        using var parcel = await counter.AddParcelAsync();
        using var money = await counter.AddMoneyOrderAsync(100m);
        using var paid = await counter.PayAsync();

        await AssertAuditedAsync(opened, AuditEvents.TransactionOpened);
        await AssertAuditedAsync(retail, AuditEvents.RetailItemAdded, counter.Id);
        await AssertAuditedAsync(parcel, AuditEvents.ShipmentAdded, counter.Id);
        await AssertAuditedAsync(money, AuditEvents.MoneyOrderAdded, counter.Id);
        var payment = await AssertAuditedAsync(paid, AuditEvents.Payment, counter.Id);
        Assert.Equal("Card", payment.RouteValues["method"]);
        Assert.Equal(StaffRole.CounterClerk, payment.Role);
        Assert.Equal("BR001", payment.Branch?.Value);
        Assert.Equal("Transactions", payment.Controller);
        Assert.Equal("Pay", payment.Action);
        Assert.NotNull(payment.StaffId);
    }

    [Fact]
    public async Task Identity_verification_void_refund_and_stock_adjustment_are_audited()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var first = await Counter.OpenAsync(clerk);
        var second = await Counter.OpenAsync(clerk);
        using var retail = await second.AddRetailAsync(Sku);
        using var paid = await second.PayAsync();

        using var verified = await first.VerifyAsync("AB1234567");
        using var voided = await first.For(manager).VoidAsync("Customer left.");
        using var refunded = await second.For(manager).RefundAsync("Damaged item.");
        using var adjusted = await manager.PostAsync(
            $"/Catalogue/Adjust?sku={Sku}",
            "/Catalogue/Adjust",
            new Dictionary<string, string> { ["Sku"] = Sku, ["Change"] = "1", ["Reason"] = "Recount." });

        await AssertAuditedAsync(verified, AuditEvents.IdentityVerification, first.Id);
        await AssertAuditedAsync(voided, AuditEvents.Void, first.Id);
        await AssertAuditedAsync(refunded, AuditEvents.Refund, second.Id);
        await AssertAuditedAsync(adjusted, AuditEvents.StockAdjustment);
    }

    [Fact]
    public async Task Sign_in_names_the_staff_member_and_sign_out_is_audited()
    {
        using var browser = new CounterBrowser(factory);

        using var signedIn = await browser.PostAsync(
            LoginPath,
            LoginPath,
            CounterBrowser.Login(DemoAccounts.Manager, ApexFactory.DemoPassword));
        using var signedOut = await browser.PostAsync("/", "/Account/Logout");

        var login = await AssertAuditedAsync(signedIn, AuditEvents.Login);
        Assert.Equal(StaffRole.BranchManager, login.Role);
        Assert.NotNull(login.StaffId);
        await AssertAuditedAsync(signedOut, AuditEvents.Logout);
    }

    [Fact]
    public async Task Failed_sign_in_is_audited_as_401_though_the_page_answers_200()
    {
        using var browser = new CounterBrowser(factory);

        using var response = await browser.PostAsync(
            LoginPath,
            LoginPath,
            CounterBrowser.Login(DemoAccounts.Clerk, "wrong-password-for-tests"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = await AuditTrail.EntryForAsync(factory, response, AuditEvents.Login);
        Assert.Equal(StatusCodes.Unauthorized, entry.StatusCode);
        Assert.Null(entry.StaffId);
    }

    [Fact]
    public async Task Denied_void_is_audited_with_the_user_and_the_real_target()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var response = await counter.VoidAsync("Not allowed.");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var entry = await AuditTrail.EntryForAsync(factory, response, AuditEvents.AccessDenied);
        Assert.Equal(StatusCodes.Forbidden, entry.StatusCode);
        Assert.Equal(StaffRole.CounterClerk, entry.Role);
        Assert.Equal("Transactions", entry.Controller);
        Assert.Equal("Void", entry.Action);
        Assert.Equal(counter.Id, entry.RouteValues["id"]);
    }

    [Fact]
    public async Task Other_error_pages_and_plain_reads_are_not_audited()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var missing = await clerk.GetAsync($"/Transactions/Details/{Guid.NewGuid()}");
        using var page = await clerk.GetAsync("/Transactions");

        var entries = await AuditTrail.EntriesAsync(factory);
        Assert.DoesNotContain(entries, entry => Matches(entry, missing) || Matches(entry, page));
    }

    [Fact]
    public async Task Error_page_cannot_be_opened_directly_to_forge_an_entry()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        using var response = await clerk.GetAsync("/Error/Status/403");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var entries = await AuditTrail.EntriesAsync(factory);
        Assert.DoesNotContain(entries, entry => Matches(entry, response));
    }

    private static bool Matches(AuditEntry entry, HttpResponseMessage response) =>
        entry.CorrelationId == Assert.Single(response.Headers.GetValues("X-Correlation-Id"));

    private async Task<AuditEntry> AssertAuditedAsync(HttpResponseMessage response, string name, string? id = null)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var entry = await AuditTrail.EntryForAsync(factory, response, name);
        Assert.Equal(StatusCodes.Found, entry.StatusCode);
        if (id is not null)
        {
            Assert.Equal(id, entry.RouteValues["id"]);
        }

        return entry;
    }

    private static class StatusCodes
    {
        public const int Found = 302;
        public const int Unauthorized = 401;
        public const int Forbidden = 403;
    }
}
