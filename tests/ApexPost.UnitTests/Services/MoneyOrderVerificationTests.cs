using ApexPost.Application.DTOs;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

/// <summary>Rule 4: a money order above 300.00 needs a passed verification in the same transaction.</summary>
public sealed class MoneyOrderVerificationTests
{
    [Theory]
    [InlineData(299.99)]
    [InlineData(300.00)]
    public async Task Amount_up_to_the_threshold_is_paid_without_verification(double amount)
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddMoneyOrderAsync(id, (decimal)amount);

        await checkout.PayAsync(id, PaymentMethod.Cash, Ct);

        Assert.Equal(TransactionStatus.Paid, (await checkout.GetAsync(id, Ct)).Status);
    }

    [Fact]
    public async Task Amount_above_the_threshold_without_verification_is_rejected()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddMoneyOrderAsync(id, 300.01m);

        Assert.True((await checkout.GetAsync(id, Ct)).RequiresVerification);
        await Assert.ThrowsAsync<DomainException>(() => checkout.PayAsync(id, PaymentMethod.Cash, Ct));
        Assert.Equal(TransactionStatus.Open, (await checkout.GetAsync(id, Ct)).Status);
    }

    [Theory]
    [InlineData(VerificationResult.Failed)]
    [InlineData(VerificationResult.Pending)]
    public async Task Verification_that_did_not_pass_does_not_allow_payment(VerificationResult result)
    {
        using var harness = new ServiceHarness();
        harness.Verifier.Result = result;
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddMoneyOrderAsync(id, 300.01m);
        await checkout.VerifyAsync(id);

        await Assert.ThrowsAsync<DomainException>(() => checkout.PayAsync(id, PaymentMethod.Cash, Ct));
    }

    [Fact]
    public async Task Passed_verification_allows_payment_and_only_the_masked_number_is_kept()
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddMoneyOrderAsync(id, 300.01m);
        await checkout.VerifyAsync(id);

        await checkout.PayAsync(id, PaymentMethod.Cash, Ct);

        var details = await checkout.GetAsync(id, Ct);
        Assert.Equal(TransactionStatus.Paid, details.Status);
        Assert.Equal("Passport ****5678 – Passed", details.Verification);
        var stored = await harness.TransactionAsync(id);
        Assert.DoesNotContain(
            stored.Verifications,
            verification => verification.MaskedNumber.Contains(TestData.DocumentNumber, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1000.00, 0)]
    [InlineData(1000.01, 1)]
    public async Task Money_order_above_the_alert_amount_raises_a_high_value_notification(double amount, int alerts)
    {
        using var harness = new ServiceHarness();
        var checkout = harness.Checkout();
        var id = await harness.OpenAsync();
        await checkout.AddMoneyOrderAsync(id, (decimal)amount);
        await checkout.VerifyAsync(id);

        await checkout.PayAsync(id, PaymentMethod.Card, Ct);

        Assert.Equal(alerts, harness.Sink.Published.Count(n => n.Kind == NotificationKind.HighValueMoneyOrder));
    }
}
