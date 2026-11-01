using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;

namespace ApexPost.UnitTests.Support;

/// <summary>Builders for valid domain objects, so each test only states what matters to it.</summary>
internal static class TestData
{
    public const string HomeBranch = "BR001";
    public const string OtherBranch = "BR002";
    public const string FirstSku = "PKG-0001";
    public const string SecondSku = "STA-0001";
    public const string DocumentNumber = "P12345678";

    public static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    public static readonly ServiceLevelMultipliers Multipliers = new(1.0m, 1.6m, 1.3m);
    public static readonly TaxRates StandardTaxRates = new(20m, 5m, 0m);

    public static Branch CreateBranch(string code = HomeBranch, bool isActive = true) =>
        new(
            new BranchCode(code),
            "Central",
            "North",
            new TimeOnly(8, 0),
            new TimeOnly(18, 0),
            [new Counter(Guid.NewGuid(), 1)],
            isActive);

    public static Staff CreateStaff(
        StaffRole role = StaffRole.CounterClerk,
        string branch = HomeBranch,
        bool isActive = true) =>
        new(Guid.NewGuid(), "Sam Taylor", "sam.taylor@apexpost.test", role, new BranchCode(branch), isActive);

    public static Product CreateProduct(
        string sku = FirstSku,
        decimal price = 10m,
        TaxCategory taxCategory = TaxCategory.Standard) =>
        new(new Sku(sku), "Padded envelope", ProductCategory.Packaging, new Money(price), taxCategory);

    public static StockItem CreateStock(string sku, int quantity, string branch = HomeBranch) =>
        new(new BranchCode(branch), new Sku(sku), quantity);

    public static PaymentRules CreateRules(decimal threshold = 300m, int lowStockThreshold = 5) =>
        new(new Money(threshold), TaxRule.From(StandardTaxRates), LowStockRule.AtOrBelow(lowStockThreshold));

    public static CounterTransaction OpenTransaction(Branch branch, Staff clerk) =>
        CounterTransaction.Open(branch, branch.Counters[0], clerk, Now);

    public static (CounterTransaction Transaction, Staff Clerk) OpenBasket()
    {
        var clerk = CreateStaff();
        return (OpenTransaction(CreateBranch(), clerk), clerk);
    }

    public static Address CreateAddress() => new()
    {
        Name = "Jo Smith",
        Street = "1 High Street",
        City = "Leeds",
        PostalCode = "LS1 1AA",
        Country = "United Kingdom",
    };

    public static Shipment CreateShipment(
        decimal kilograms,
        ShipmentType type = ShipmentType.Parcel,
        ServiceLevel level = ServiceLevel.Standard,
        Dimensions? dimensions = null) =>
        new(type, level, Weight.FromKilograms(kilograms), dimensions ?? new Dimensions(10m, 10m, 10m), CreateAddress());

    public static ShippingZone CreateZone(decimal baseRate = 5m, decimal perKgRate = 1.5m) =>
        new("Z01", "Domestic", new Money(baseRate), new Money(perKgRate));

    public static IdentityVerification CreateVerification(VerificationResult result) =>
        IdentityVerification.FromDocument(DocumentType.Passport, DocumentNumber, result, Now);
}
