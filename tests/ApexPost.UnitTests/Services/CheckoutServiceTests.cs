using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

public sealed class CheckoutServiceTests
{
    [Fact]
    public async Task Clerk_builds_a_mixed_basket_and_pays()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku, 3);
        await checkout.AddParcelAsync(id, 2m);
        await checkout.AddMoneyOrderAsync(id, 50m);

        await checkout.PayAsync(id, PaymentMethod.Card, Ct);

        var details = await checkout.GetAsync(id, Ct);
        Assert.Equal(TransactionStatus.Paid, details.Status);
        Assert.Equal([LineKind.Retail, LineKind.Shipping, LineKind.MoneyOrder], details.Lines.Select(l => l.Kind));
        Assert.Equal(new Money(30.00m), details.RetailTotal);
        Assert.Equal(new Money(60.00m), details.ServicesTotal);
        Assert.Equal(new Money(7.60m), details.Tax);
        Assert.Equal(new Money(97.60m), details.Total);
        Assert.Equal(7, await harness.StockOfAsync(ServiceHarness.StandardSku));
    }

    [Fact]
    public async Task Payment_fails_without_enough_stock_and_changes_nothing()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku, 2);
        await checkout.AddRetailAsync(id, ServiceHarness.ReducedSku, ServiceHarness.InitialStock + 1);

        await Assert.ThrowsAsync<InsufficientStockException>(() => checkout.PayAsync(id, PaymentMethod.Card, Ct));

        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.StandardSku));
        Assert.Equal(ServiceHarness.InitialStock, await harness.StockOfAsync(ServiceHarness.ReducedSku));
        Assert.Equal(TransactionStatus.Open, (await harness.TransactionAsync(id)).Status);
    }

    [Fact]
    public async Task Selling_down_to_the_threshold_sends_a_low_stock_notification()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku, ServiceHarness.InitialStock - 5);

        await checkout.PayAsync(id, PaymentMethod.Card, Ct);

        var notification = Assert.Single(harness.Sink.Published);
        Assert.Equal(NotificationKind.LowStock, notification.Kind);
        Assert.Contains(ServiceHarness.StandardSku, notification.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stock_above_the_threshold_sends_no_notification()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku, ServiceHarness.InitialStock - 6);

        await checkout.PayAsync(id, PaymentMethod.Card, Ct);

        Assert.Empty(harness.Sink.Published);
    }

    [Fact]
    public async Task Parcel_over_the_weight_limit_of_its_level_is_rejected()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();

        await Assert.ThrowsAsync<DomainException>(() => checkout.AddParcelAsync(id, 10.001m, ServiceLevel.Registered));
        await checkout.AddParcelAsync(id, 10m, ServiceLevel.Registered);

        Assert.Single((await checkout.GetAsync(id, Ct)).Lines);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_with_field_errors()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            checkout.AddRetailItemAsync(id, new AddRetailItemCommand { Sku = "nope", Quantity = 0 }, Ct));

        Assert.Equal(["Quantity", "Sku"], error.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Unknown_product_is_not_found()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => harness.Checkout().AddRetailAsync(id, "PKG-9999"));
    }

    [Fact]
    public async Task Closed_branch_cannot_open_a_transaction()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.ClosedBranchClerk);

        await Assert.ThrowsAsync<DomainException>(() =>
            harness.Checkout().OpenAsync(harness.ClosedBranch.Counters[0].Id, Ct));
    }

    [Fact]
    public async Task Inactive_clerk_cannot_open_a_transaction()
    {
        using var harness = new ServiceHarness();
        var inactive = harness.StaffMembers.Seed(new Staff(
            Guid.NewGuid(),
            "Kim Doe",
            "clerk4.br001@apexpost.test",
            StaffRole.CounterClerk,
            new BranchCode(TestData.HomeBranch),
            isActive: false));
        harness.As(inactive);

        await Assert.ThrowsAsync<DomainException>(harness.OpenAsync);
    }

    [Theory]
    [InlineData(StaffRole.Auditor)]
    [InlineData(StaffRole.Administrator)]
    public async Task Auditors_and_administrators_cannot_transact(StaffRole role)
    {
        using var harness = new ServiceHarness();
        harness.As(role == StaffRole.Auditor ? harness.Auditor : harness.Admin);

        await Assert.ThrowsAsync<ForbiddenException>(harness.OpenAsync);
    }

    [Fact]
    public async Task Counter_of_another_branch_is_not_found()
    {
        using var harness = new ServiceHarness();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.Checkout().OpenAsync(harness.OtherBranch.Counters[0].Id, Ct));
    }

    [Fact]
    public async Task Clerk_of_another_branch_cannot_see_or_change_the_transaction()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();
        harness.As(harness.OtherClerk);
        var checkout = harness.Checkout();

        await Assert.ThrowsAsync<ForbiddenException>(() => checkout.GetAsync(id, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => checkout.AddRetailAsync(id, ServiceHarness.StandardSku));
        await Assert.ThrowsAsync<ForbiddenException>(() => checkout.PayAsync(id, PaymentMethod.Cash, Ct));
    }

    [Fact]
    public async Task Auditor_may_read_a_transaction_of_any_branch()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();
        harness.As(harness.Auditor);

        Assert.Equal(id, (await harness.Checkout().GetAsync(id, Ct)).Id);
    }

    [Fact]
    public async Task Clerk_list_only_contains_the_own_branch()
    {
        using var harness = new ServiceHarness();
        await harness.OpenAsync();
        harness.As(harness.OtherClerk);
        await harness.Checkout().OpenAsync(harness.OtherBranch.Counters[0].Id, Ct);

        var list = await harness.Checkout().ListAsync(new TransactionFilter(), new PageRequest(), Ct);

        Assert.Equal(new BranchCode(TestData.OtherBranch), Assert.Single(list.Items).Branch);
        await Assert.ThrowsAsync<ForbiddenException>(() => harness.Checkout().ListAsync(
            new TransactionFilter(new BranchCode(TestData.HomeBranch)),
            new PageRequest(),
            Ct));
    }

    [Fact]
    public async Task Label_shows_reference_tracking_number_and_recipient()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddRetailAsync(id, ServiceHarness.StandardSku);
        var lineId = await checkout.AddParcelAsync(id, 2m, ServiceLevel.Express);

        var label = await checkout.GetLabelAsync(id, lineId, Ct);

        Assert.Matches("^APX-[0-9A-F]{8}-2$", label.Reference);
        Assert.StartsWith("AP", label.TrackingNumber, StringComparison.Ordinal);
        Assert.Equal("Jo Smith", label.Recipient.Name);
        Assert.Equal(ServiceLevel.Express, label.Level);
        await Assert.ThrowsAsync<NotFoundException>(() => checkout.GetLabelAsync(id, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Concurrent_change_of_the_same_basket_is_a_conflict()
    {
        using var harness = new ServiceHarness();
        var id = await harness.OpenAsync();
        var stale = await harness.TransactionAsync(id);
        await harness.Checkout().AddRetailAsync(id, ServiceHarness.StandardSku);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            harness.Transactions.UpdateAsync(stale, stale.Version, Ct));
    }
}
