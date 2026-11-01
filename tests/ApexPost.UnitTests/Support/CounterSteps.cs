using ApexPost.Application.DTOs;
using ApexPost.Application.Services;
using ApexPost.Domain.Shipping;

namespace ApexPost.UnitTests.Support;

/// <summary>Short-hands for the counter steps most service tests repeat.</summary>
internal static class CounterSteps
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task<Guid> OpenAsync(this ServiceHarness harness) =>
        harness.Checkout().OpenAsync(harness.Branch.Counters[0].Id, Ct);

    public static Task<Guid> AddRetailAsync(this CheckoutService checkout, Guid id, string sku, int quantity = 1) =>
        checkout.AddRetailItemAsync(id, new AddRetailItemCommand { Sku = sku, Quantity = quantity }, Ct);

    public static Task<Guid> AddMoneyOrderAsync(this CheckoutService checkout, Guid id, decimal amount) =>
        checkout.AddMoneyOrderAsync(
            id,
            new AddMoneyOrderCommand { Amount = amount, Sender = "Ann Lee", Recipient = "Bo Chan" },
            Ct);

    public static Task<Guid> AddParcelAsync(
        this CheckoutService checkout,
        Guid id,
        decimal kilograms,
        ServiceLevel level = ServiceLevel.Standard,
        decimal lengthCm = 10m,
        decimal widthCm = 10m,
        decimal heightCm = 10m) =>
        checkout.AddShippingAsync(id, Parcel(kilograms, level, lengthCm, widthCm, heightCm), Ct);

    public static AddShippingCommand Parcel(
        decimal kilograms,
        ServiceLevel level = ServiceLevel.Standard,
        decimal lengthCm = 10m,
        decimal widthCm = 10m,
        decimal heightCm = 10m,
        ShipmentType type = ShipmentType.Parcel) =>
        new()
        {
            Type = type,
            Level = level,
            WeightKg = kilograms,
            LengthCm = lengthCm,
            WidthCm = widthCm,
            HeightCm = heightCm,
            ZoneCode = ServiceHarness.ZoneCode,
            RecipientName = "Jo Smith",
            Street = "1 High Street",
            City = "Leeds",
            PostalCode = "LS1 1AA",
            Country = "United Kingdom",
        };

    public static Task VerifyAsync(this CheckoutService checkout, Guid id) =>
        checkout.VerifyIdentityAsync(id, new VerifyIdentityCommand { DocumentNumber = TestData.DocumentNumber }, Ct);
}
