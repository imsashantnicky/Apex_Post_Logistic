using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Filters;

public sealed class InlineValidationTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string CreatePath = "/Staff/Create";
    private const string TypedPassword = "Typed-password-for-tests";
    private const string TypedDocument = "AB!1234567";

    [Fact]
    public async Task Refused_staff_form_shows_the_error_at_the_field_and_keeps_what_was_typed_but_the_password()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);

        using var response = await PostStaffAsync(admin, DemoAccounts.Clerk);
        var page = await admin.ReadPageAsync(response.Headers.Location!.OriginalString);

        Assert.Equal(CreatePath, response.Headers.Location?.OriginalString);
        Assert.Contains("<span class=\"field-error\" id=\"Email-error\">", page, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"Email-error\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-invalid=\"true\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"Nia Brooks\"", page, StringComparison.Ordinal);
        Assert.Matches("<option[^>]*selected[^>]*value=\"BR003\"|<option value=\"BR003\"[^>]*selected", page);
        Assert.Matches("<option[^>]*selected[^>]*value=\"Auditor\"|<option value=\"Auditor\"[^>]*selected", page);
        Assert.DoesNotContain(TypedPassword, page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remembered_form_lasts_for_one_page()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        using var response = await PostStaffAsync(admin, DemoAccounts.Clerk);

        await admin.ReadPageAsync(CreatePath);
        var again = await admin.ReadPageAsync(CreatePath);

        Assert.DoesNotContain("field-error", again, StringComparison.Ordinal);
        Assert.DoesNotContain("Nia Brooks", again, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Document_number_of_a_refused_check_is_not_shown_again()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var response = await counter.VerifyAsync(TypedDocument);
        var page = await counter.PageAsync();

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("id=\"DocumentNumber-error\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain(TypedDocument, page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Broken_business_rule_keeps_the_typed_shipment()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var response = await counter.AddParcelAsync(weightKg: 31m, level: "Express");
        var page = await counter.PageAsync();

        Assert.Contains("value=\"Bristol\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"31\"", page, StringComparison.Ordinal);
        Assert.Matches("<option[^>]*selected[^>]*value=\"Express\"|<option value=\"Express\"[^>]*selected", page);
    }

    [Fact]
    public async Task Same_field_of_another_form_on_the_page_stays_clean()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        using var added = await counter.AddRetailAsync("PKG-0001");
        using var paid = await counter.PayAsync();
        var managerCounter = counter.For(manager);

        using var response = await managerCounter.VoidAsync("   ");
        var page = await managerCounter.PageAsync();

        Assert.Contains("id=\"VoidReason-error\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"RefundReason-error\"", page, StringComparison.Ordinal);
        Assert.Contains("<details open=\"open\">", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_form_does_not_bring_old_values_back()
    {
        using var admin = await AdminBrowser.SignInAsync(factory, DemoAccounts.Administrator);
        var member = await factory.StaffAsync("clerk1.br003@apexpost.test");

        using var response = await admin.PostAsync("/Staff", $"/Staff/Edit/{member.Id}", new Dictionary<string, string>
        {
            ["version"] = "999",
            ["Name"] = "Stale Name",
            ["Email"] = member.Email,
            ["Role"] = member.Role.ToString(),
            ["HomeBranch"] = member.HomeBranch.Value,
        });
        var page = await admin.ReadPageAsync(response.Headers.Location!.OriginalString);

        Assert.Contains("changed by someone else", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Stale Name", page, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> PostStaffAsync(CounterBrowser admin, string email) =>
        admin.PostAsync(CreatePath, CreatePath, new Dictionary<string, string>
        {
            ["Name"] = "Nia Brooks",
            ["Email"] = email,
            ["Role"] = "Auditor",
            ["HomeBranch"] = "BR003",
            ["Password"] = TypedPassword,
        });
}
