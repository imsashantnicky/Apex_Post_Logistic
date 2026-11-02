using System.Globalization;
using System.Net;
using ApexPost.IntegrationTests.Support;
using Microsoft.Data.SqlClient;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>The real application on the role of sql/security, and what that role must not be able to do.</summary>
public sealed class LeastPrivilegeTests(LeastPrivilegeFactory factory) : IClassFixture<LeastPrivilegeFactory>
{
    private const int PermissionDenied = 229;
    private const int CannotCreateInDatabase = 262;
    private const int CannotDropTable = 3701;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(DemoAccounts.Manager, "/Dashboard")]
    [InlineData(DemoAccounts.Manager, "/Catalogue")]
    [InlineData(DemoAccounts.Manager, "/Transactions")]
    [InlineData(DemoAccounts.Manager, "/Reports/DailySales")]
    [InlineData(DemoAccounts.Manager, "/Reports/LowStock")]
    [InlineData(DemoAccounts.Manager, "/Reports/ShippingVolume")]
    [InlineData(DemoAccounts.Manager, "/Reports/MoneyOrders")]
    [InlineData(DemoAccounts.Auditor, "/Reports/DailySales")]
    public async Task Reading_pages_need_no_more_than_the_role(string email, string path)
    {
        using var server = factory.ApplicationServer();
        using var browser = await CounterBrowser.SignInAsync(server, email);

        using var page = await browser.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task The_audit_log_is_readable_on_the_role()
    {
        using var server = factory.ApplicationServer();
        using var auditor = await AdminBrowser.SignInAsync(server, DemoAccounts.Auditor);

        using var page = await auditor.GetAsync("/Audit");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task A_sale_with_every_kind_of_line_and_its_refund_work_on_the_role()
    {
        using var server = factory.ApplicationServer();
        using var clerk = await CounterBrowser.SignInAsync(server, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(server, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        using var retail = await counter.AddRetailAsync("PKG-0002");
        using var parcel = await counter.AddParcelAsync();
        using var order = await counter.AddMoneyOrderAsync(350m);
        using var check = await counter.VerifyAsync("AB1234567");

        using var paid = await counter.PayAsync();
        using var refunded = await counter.For(manager).RefundAsync("Returned at the counter.");

        Assert.Contains("Refunded", await counter.For(manager).PageAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, refunded.StatusCode);
    }

    [Fact]
    public async Task A_stock_adjustment_writes_its_history_through_the_procedure_alone()
    {
        const string sku = "OFF-0004";
        using var server = factory.ApplicationServer();
        using var manager = await CounterBrowser.SignInAsync(server, DemoAccounts.Manager);
        var before = await factory.QuantityAsync(sku);

        using var added = await manager.PostAsync(
            $"/Catalogue/Adjust?sku={sku}",
            "/Catalogue/Adjust",
            new Dictionary<string, string> { ["Sku"] = sku, ["Change"] = "3", ["Reason"] = "Delivery counted." });

        Assert.Equal("/Catalogue", added.Headers.Location!.OriginalString);
        Assert.Equal(before + 3, await factory.QuantityAsync(sku));
    }

    [Fact]
    public async Task Administration_works_on_the_role()
    {
        const string code = "BR004";
        using var server = factory.ApplicationServer();
        using var admin = await AdminBrowser.SignInAsync(server, DemoAccounts.Administrator);
        var details = $"/Branches/Details/{code}";
        var version = await factory.BranchAsync(code);

        using var added = await admin.PostAsync(
            details,
            $"/Branches/AddCounter/{code}",
            new Dictionary<string, string> { ["version"] = Text(version.Version) });
        var withNew = await factory.BranchAsync(code);
        using var removed = await admin.PostAsync(
            details,
            $"/Branches/RemoveCounter/{code}",
            new Dictionary<string, string>
            {
                ["counterId"] = withNew.Counters[^1].Id.ToString(),
                ["version"] = Text(withNew.Version),
            });
        using var staff = await admin.PostAsync("/Staff/Create", "/Staff/Create", new Dictionary<string, string>
        {
            ["Name"] = "Role Test",
            ["Email"] = "role.test@apexpost.test",
            ["Role"] = "CounterClerk",
            ["HomeBranch"] = code,
            ["Password"] = "A-long-password-for-role-tests",
        });

        Assert.Equal(version.Counters.Count + 1, withNew.Counters.Count);
        Assert.Equal(version.Counters.Count, (await factory.BranchAsync(code)).Counters.Count);
        Assert.Equal(HttpStatusCode.Redirect, staff.StatusCode);
    }

    [Theory]
    [InlineData("DELETE FROM dbo.AuditEntries", PermissionDenied)]
    [InlineData("UPDATE dbo.AuditEntries SET StatusCode = 200", PermissionDenied)]
    [InlineData("DELETE FROM dbo.StockAdjustments", PermissionDenied)]
    [InlineData("INSERT dbo.Roles (Name) VALUES ('Owner')", PermissionDenied)]
    [InlineData("UPDATE dbo.TransactionLines SET Quantity = 1", PermissionDenied)]
    [InlineData("DELETE FROM dbo.Payments", PermissionDenied)]
    [InlineData("SELECT * FROM dbo.__EFMigrationsHistory", PermissionDenied)]
    [InlineData("CREATE TABLE dbo.Stolen (Id int)", CannotCreateInDatabase)]
    [InlineData("DROP TABLE dbo.Staff", CannotDropTable)]
    public async Task What_the_application_never_does_is_refused(string statement, int errorNumber)
    {
        await using var connection = await factory.OpenAsRestrictedUserAsync();
        await using var command = new SqlCommand(statement, connection);

        var refused = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync(Ct));

        Assert.Equal(errorNumber, refused.Number);
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
