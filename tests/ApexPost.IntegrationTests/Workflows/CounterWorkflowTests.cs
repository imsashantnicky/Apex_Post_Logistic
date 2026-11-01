using System.Net;
using System.Text.RegularExpressions;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Workflows;

public sealed partial class CounterWorkflowTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string Sku = "PKG-0001";
    private const string SecondSku = "PKG-0002";
    private const string ThirdSku = "PKG-0003";
    private const string Reason = "Customer changed their mind.";
    private const string DocumentNumber = "X98765432";

    [Fact]
    public async Task Mixed_basket_previews_tax_and_is_paid_with_redirects_after_every_post()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var before = await factory.QuantityAsync(Sku);
        var counter = await Counter.OpenAsync(clerk);

        AssertBackOnCounter(counter, await counter.AddRetailAsync(Sku, 2));
        AssertBackOnCounter(counter, await counter.AddParcelAsync());
        AssertBackOnCounter(counter, await counter.AddMoneyOrderAsync(100m));
        var open = await counter.PageAsync();
        AssertBackOnCounter(counter, await counter.PayAsync());
        var paid = await counter.PageAsync();

        Assert.Contains("Tax (preview)", open, StringComparison.Ordinal);
        Assert.NotEqual("0.00 USD", TaxOf(open));
        Assert.Contains("pill-paid", paid, StringComparison.Ordinal);
        Assert.Contains("Paid by Card", paid, StringComparison.Ordinal);
        Assert.Equal(TaxOf(open), TaxOf(paid));
        Assert.Equal(before - 2, await factory.QuantityAsync(Sku));
    }

    [Fact]
    public async Task Label_preview_shows_reference_addresses_weight_and_service()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        await counter.AddParcelAsync(level: "Express");

        var label = await clerk.ReadPageAsync(await counter.LabelLinkAsync());

        Assert.Matches("APX-[0-9A-F]{8}-1", label);
        Assert.Contains("Central Counter", label, StringComparison.Ordinal);
        Assert.Contains("Hannah Reid", label, StringComparison.Ordinal);
        Assert.Contains("2.000 kg", label, StringComparison.Ordinal);
        Assert.Contains("Express Parcel", label, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Money_order_above_the_threshold_needs_a_passed_verification()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        await counter.AddMoneyOrderAsync(300.01m);

        AssertBackOnCounter(counter, await counter.PayAsync());
        var unverified = await counter.PageAsync();
        await counter.VerifyAsync("FAIL123456");
        await counter.PayAsync();
        var failed = await counter.PageAsync();
        await counter.VerifyAsync(DocumentNumber);
        await counter.PayAsync();
        var paid = await counter.PageAsync();

        Assert.Contains("needs a passed identity check", unverified, StringComparison.Ordinal);
        Assert.Contains("pill-failed", failed, StringComparison.Ordinal);
        Assert.Contains("pill-open", failed, StringComparison.Ordinal);
        Assert.Contains("pill-paid", paid, StringComparison.Ordinal);
        Assert.Contains("Identity: Passport ****5432", paid, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Money_order_of_exactly_the_threshold_is_paid_without_verification()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        await counter.AddMoneyOrderAsync(300m);

        await counter.PayAsync("Cash");

        Assert.Contains("pill-paid", await counter.PageAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Full_document_number_is_never_stored_or_shown()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        await counter.VerifyAsync(DocumentNumber);

        Assert.DoesNotContain(DocumentNumber, await AuditTrail.RawChecksAsync(factory), StringComparison.Ordinal);
        Assert.DoesNotContain(DocumentNumber, await counter.PageAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Short_stock_keeps_the_basket_open_and_the_stock_unchanged()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var before = await factory.QuantityAsync(SecondSku);
        var counter = await Counter.OpenAsync(clerk);
        await counter.AddRetailAsync(ThirdSku);
        await counter.AddRetailAsync(SecondSku, before + 1);

        AssertBackOnCounter(counter, await counter.PayAsync());

        var page = await counter.PageAsync();
        Assert.Contains("alert-bad", page, StringComparison.Ordinal);
        Assert.Contains("pill-open", page, StringComparison.Ordinal);
        Assert.Equal(before, await factory.QuantityAsync(SecondSku));
    }

    [Fact]
    public async Task Parcel_over_the_service_limit_is_rejected()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        AssertBackOnCounter(counter, await counter.AddParcelAsync(20.001m, "Express"));

        var page = await counter.PageAsync();
        Assert.Contains("alert-bad", page, StringComparison.Ordinal);
        Assert.Contains("The basket is empty.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zero_money_order_shows_the_validation_message()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        await counter.AddMoneyOrderAsync(0m);

        var page = await counter.PageAsync();
        Assert.Contains("Amount must be greater than zero.", page, StringComparison.Ordinal);
        Assert.Contains("The basket is empty.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_in_a_number_field_is_reported_instead_of_using_a_default()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var response = await clerk.PostAsync(
            counter.Path,
            counter.Path.Replace("Details", "AddRetail", StringComparison.Ordinal),
            new Dictionary<string, string> { ["Sku"] = Sku, ["Quantity"] = "many" });

        AssertBackOnCounter(counter, response);
        var page = await counter.PageAsync();
        Assert.Contains("One or more fields are not valid.", page, StringComparison.Ordinal);
        Assert.Contains("The basket is empty.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Manager_void_of_a_paid_transaction_returns_the_stock_and_raises_an_alert()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        const string sku = "STA-0001";
        var before = await factory.QuantityAsync(sku);
        var counter = await Counter.OpenAsync(clerk);
        await counter.AddRetailAsync(sku, 3);
        await counter.PayAsync();
        var managerCounter = counter.For(manager);

        AssertBackOnCounter(managerCounter, await managerCounter.VoidAsync(Reason));

        var page = await managerCounter.PageAsync();
        Assert.Contains("pill-voided", page, StringComparison.Ordinal);
        Assert.Contains(Reason, page, StringComparison.Ordinal);
        Assert.Equal(before, await factory.QuantityAsync(sku));
        Assert.Contains("was voided", await manager.ReadPageAsync("/"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Manager_refund_needs_a_reason_and_a_paid_transaction()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        const string sku = "STA-0002";
        var before = await factory.QuantityAsync(sku);
        var counter = await Counter.OpenAsync(clerk);
        await counter.AddRetailAsync(sku, 2);
        await counter.PayAsync();
        var managerCounter = counter.For(manager);

        AssertBackOnCounter(managerCounter, await managerCounter.RefundAsync("   "));
        var withoutReason = await managerCounter.PageAsync();
        await managerCounter.RefundAsync(Reason);
        var refunded = await managerCounter.PageAsync();

        Assert.Contains("pill-paid", withoutReason, StringComparison.Ordinal);
        Assert.Contains("pill-refunded", refunded, StringComparison.Ordinal);
        Assert.Equal(before, await factory.QuantityAsync(sku));
    }

    [Fact]
    public async Task Closed_branch_cannot_open_a_transaction()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.ClosedBranchManager);
        const string newPath = "/Transactions/New";

        using var response = await Counter.PostOpenAsync(manager);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(newPath, response.Headers.Location!.OriginalString);
        var page = await manager.ReadPageAsync(newPath);
        Assert.Contains("is closed and cannot accept transactions", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Manager_adjusts_stock_with_a_reason()
    {
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        const string sku = "OFF-0002";
        var path = $"/Catalogue/Adjust?sku={sku}";
        var before = await factory.QuantityAsync(sku);

        using var added = await manager.PostAsync(path, "/Catalogue/Adjust", new Dictionary<string, string>
        {
            ["Sku"] = sku,
            ["Change"] = "4",
            ["Reason"] = "Delivery counted.",
        });
        using var tooMany = await manager.PostAsync(path, "/Catalogue/Adjust", new Dictionary<string, string>
        {
            ["Sku"] = sku,
            ["Change"] = "-100000",
            ["Reason"] = "Stocktake.",
        });

        Assert.Equal("/Catalogue", added.Headers.Location!.OriginalString);
        Assert.Equal(path, tooMany.Headers.Location!.OriginalString);
        Assert.Equal(before + 4, await factory.QuantityAsync(sku));
    }

    [Fact]
    public async Task Catalogue_search_and_category_filter_narrow_the_list()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);

        var searched = await clerk.ReadPageAsync($"/Catalogue?search={Sku}");
        var filtered = await clerk.ReadPageAsync("/Catalogue?category=Stationery");

        Assert.Contains(Sku, searched, StringComparison.Ordinal);
        Assert.DoesNotContain(SecondSku, searched, StringComparison.Ordinal);
        Assert.DoesNotContain(Sku, filtered, StringComparison.Ordinal);
        Assert.Contains("STA-0001", filtered, StringComparison.Ordinal);
    }

    private static string TaxOf(string page) => TaxRow().Match(page).Groups[1].Value;

    private static void AssertBackOnCounter(Counter counter, HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(counter.Path, response.Headers.Location!.OriginalString);
        }
    }

    [GeneratedRegex("<dt>Tax[^<]*</dt>\\s*<dd><span class=\"money\">([^<]+)")]
    private static partial Regex TaxRow();
}
