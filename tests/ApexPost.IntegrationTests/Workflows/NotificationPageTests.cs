using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Workflows;

public sealed class NotificationPageTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string NotificationsPath = "/Notifications";
    private const string Reason = "Customer changed their mind.";

    [Fact]
    public async Task Void_is_shown_to_the_branch_and_the_auditor_but_not_to_another_branch()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        using var otherManager = await CounterBrowser.SignInAsync(factory, DemoAccounts.OtherBranchManager);
        using var auditor = await CounterBrowser.SignInAsync(factory, DemoAccounts.Auditor);
        var counter = await Counter.OpenAsync(clerk);

        using var voided = await counter.For(manager).VoidAsync(Reason);

        Assert.Contains(Reason, await clerk.ReadPageAsync(NotificationsPath), StringComparison.Ordinal);
        Assert.Contains(Reason, await manager.ReadPageAsync(NotificationsPath), StringComparison.Ordinal);
        Assert.Contains(Reason, await auditor.ReadPageAsync(NotificationsPath), StringComparison.Ordinal);
        Assert.Contains(
            Reason,
            await auditor.ReadPageAsync($"{NotificationsPath}?branch=BR001"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            Reason,
            await auditor.ReadPageAsync($"{NotificationsPath}?branch=BR002"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(Reason, await otherManager.ReadPageAsync(NotificationsPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task High_value_money_order_and_refund_are_listed_with_their_kind()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.SecondClerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        using var order = await counter.AddMoneyOrderAsync(1500m);
        using var verified = await counter.VerifyAsync("AB1234567");
        using var paid = await counter.PayAsync();
        using var refunded = await counter.For(manager).RefundAsync("Entered twice.");

        var page = await manager.ReadPageAsync(NotificationsPath);

        Assert.Contains("High Value Money Order", page, StringComparison.Ordinal);
        Assert.Contains("Refund", page, StringComparison.Ordinal);
        Assert.Contains("Entered twice.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dashboard_alert_card_links_to_the_notifications()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        using var manager = await CounterBrowser.SignInAsync(factory, DemoAccounts.Manager);
        var counter = await Counter.OpenAsync(clerk);
        using var voided = await counter.For(manager).VoidAsync(Reason);

        var dashboard = await manager.ReadPageAsync("/");

        Assert.Contains("All notifications", dashboard, StringComparison.Ordinal);
        Assert.Contains(Reason, dashboard, StringComparison.Ordinal);
    }
}
