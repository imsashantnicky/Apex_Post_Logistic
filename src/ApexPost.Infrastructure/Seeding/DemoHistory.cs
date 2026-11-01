using System.Globalization;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>
/// 48 past transactions over the 14 days before today, spread over the open branches and built through the
/// domain methods, so every business rule holds and the sold goods leave the seeded stock. The mix: retail,
/// parcels, letters, money orders (some above the verification threshold, one failed check), voids and refunds.
/// </summary>
public sealed class DemoHistory(
    IReadOnlyList<Branch> branches,
    IReadOnlyList<Staff> staff,
    IReadOnlyList<Product> products,
    IReadOnlyList<ShippingZone> zones,
    IReadOnlyCollection<StockItem> stock,
    PaymentRules rules,
    ServiceLevelMultipliers multipliers)
{
    public const int TransactionCount = 48;
    public const int DayCount = 14;

    private const int Kinds = 6;
    private const int ReversalCycle = 12;
    private const int VoidSlot = 7;
    private const int RefundSlot = 10;
    private const int FailedCheckSlot = 9;
    private const int AbandonedIndex = 41;
    private const int MinutesAfterOpening = 30;
    private const int MinutesBetweenCustomers = 45;
    private const int CustomersPerDay = 7;
    private const int MinutesToPay = 4;
    private const int MinutesToVoid = 40;
    private const int HoursToRefund = 2;
    private const int MaxQuantity = 3;
    private const string VoidReason = "Customer changed their mind at the counter.";
    private const string RefundReason = "Damaged item returned with receipt.";
    private const string AbandonedReason = "Basket abandoned; customer left.";

    private static readonly decimal[] SmallOrders = [50m, 120m, 200m, 250m];
    private static readonly decimal[] LargeOrders = [450m, 1250m, 800m];
    private static readonly ServiceLevel[] Levels =
        [ServiceLevel.Standard, ServiceLevel.Express, ServiceLevel.Registered];
    private static readonly DocumentType[] Documents =
        [DocumentType.Passport, DocumentType.NationalIdCard, DocumentType.DrivingLicence];

    private static readonly (string Name, string Street, string City, string PostalCode, string Country)[] Recipients =
    [
        ("Hannah Reid", "12 Mill Lane", "Bristol", "BS1 4DJ", "United Kingdom"),
        ("Marco Bianchi", "Via Roma 8", "Milan", "20121", "Italy"),
        ("Priya Nair", "44 Lake Road", "Toronto", "M5V 2T6", "Canada"),
        ("Kenji Sato", "3-1 Marunouchi", "Tokyo", "100-0005", "Japan"),
    ];

    private readonly Product[] _popular = [.. products.Where((_, index) => !DemoStock.IsKeptLow(index))];

    public IReadOnlyList<CounterTransaction> Create(DateTimeOffset now)
    {
        Branch[] open = [.. branches.Where(branch => branch.IsActive)];
        return
        [
            .. Enumerable.Range(0, TransactionCount).Select(index => Create(index, open[index % open.Length], now)),
        ];
    }

    private CounterTransaction Create(int index, Branch branch, DateTimeOffset now)
    {
        var clerk = ClerkOf(branch, index);
        var openedAt = OpenedAt(index, branch, now);
        var counter = branch.Counters[index % branch.Counters.Count];
        var transaction = CounterTransaction.Open(branch, counter, clerk, openedAt);
        foreach (var line in LinesFor(index))
        {
            transaction.AddLine(clerk, line);
        }

        Verify(transaction, clerk, index, openedAt);
        if (index == AbandonedIndex)
        {
            transaction.Void(ManagerOf(branch), AbandonedReason, stock, openedAt.AddMinutes(MinutesToVoid));
            return transaction;
        }

        var paidAt = openedAt.AddMinutes(MinutesToPay);
        var method = index % 2 == 0 ? PaymentMethod.Card : PaymentMethod.Cash;
        transaction.Pay(clerk, method, rules, stock, paidAt);
        Reverse(transaction, branch, index, paidAt);
        transaction.TakeEvents();
        return transaction;
    }

    private ILineItem[] LinesFor(int index) => (index % Kinds) switch
    {
        0 => [Retail(index, 0), Retail(index, 1)],
        1 => [Parcel(index), Retail(index, 0)],
        2 => [MoneyOrder(SmallOrders[index % SmallOrders.Length], index)],
        3 => [MoneyOrder(LargeOrders[index % LargeOrders.Length], index)],
        4 => [Letter(index), Retail(index, 2)],
        _ => [Retail(index, 1), Parcel(index), MoneyOrder(SmallOrders[0], index)],
    };

    private void Verify(CounterTransaction transaction, Staff clerk, int index, DateTimeOffset openedAt)
    {
        if (!transaction.RequiresVerification(rules.VerificationThreshold))
        {
            return;
        }

        var document = Documents[index % Documents.Length];
        if (index % ReversalCycle == FailedCheckSlot)
        {
            transaction.RecordVerification(
                clerk,
                IdentityVerification.FromDocument(
                    document,
                    DocumentNumber(index + 1),
                    VerificationResult.Failed,
                    openedAt));
        }

        transaction.RecordVerification(
            clerk,
            IdentityVerification.FromDocument(
                document,
                DocumentNumber(index),
                VerificationResult.Passed,
                openedAt));
    }

    private void Reverse(CounterTransaction transaction, Branch branch, int index, DateTimeOffset paidAt)
    {
        switch (index % ReversalCycle)
        {
            case VoidSlot:
                transaction.Void(ManagerOf(branch), VoidReason, stock, paidAt.AddMinutes(MinutesToVoid));
                break;
            case RefundSlot:
                transaction.Refund(ManagerOf(branch), RefundReason, stock, paidAt.AddHours(HoursToRefund));
                break;
            default:
                break;
        }
    }

    private RetailLineItem Retail(int index, int slot)
    {
        var product = _popular[((index / branches.Count) + (slot * MaxQuantity)) % _popular.Length];
        return RetailLineItem.For(product, 1 + ((index + slot) % MaxQuantity));
    }

    private ShippingLineItem Parcel(int index)
    {
        var kilograms = 0.5m + (index % 8 * 0.75m);
        var size = new Dimensions(20m + (index % 10), 15m, 10m);
        var shipment = new Shipment(
            ShipmentType.Parcel,
            Levels[index % Levels.Length],
            Weight.FromKilograms(kilograms),
            size,
            Recipient(index));
        return ShippingLineItem.Create(shipment, zones[index % zones.Count], multipliers);
    }

    private ShippingLineItem Letter(int index)
    {
        var shipment = new Shipment(
            ShipmentType.Letter,
            ServiceLevel.Standard,
            new Weight(20 + (index * 5)),
            new Dimensions(24m, 16m, 0.5m),
            Recipient(index));
        return ShippingLineItem.Create(shipment, zones[index % zones.Count], multipliers);
    }

    private MoneyOrderLineItem MoneyOrder(decimal amount, int index) =>
        MoneyOrderLineItem.Issue(new Money(amount), staff[index % staff.Count].Name, Recipient(index).Name);

    internal static Address Recipient(int index)
    {
        var row = Recipients[index % Recipients.Length];
        return new Address
        {
            Name = row.Name,
            Street = row.Street,
            City = row.City,
            PostalCode = row.PostalCode,
            Country = row.Country,
        };
    }

    internal static string DocumentNumber(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"D{1_000_000 + (index * 7919)}");

    private static DateTimeOffset OpenedAt(int index, Branch branch, DateTimeOffset now)
    {
        var daysAgo = DayCount - (index * DayCount / TransactionCount);
        var day = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-daysAgo);
        var minutes = MinutesAfterOpening + (index % CustomersPerDay * MinutesBetweenCustomers);
        return new DateTimeOffset(day.ToDateTime(branch.OpensAt.AddMinutes(minutes)), TimeSpan.Zero);
    }

    private Staff ClerkOf(Branch branch, int index)
    {
        Staff[] clerks =
        [
            .. staff.Where(member => member.HomeBranch == branch.Code
                && member.Role == StaffRole.CounterClerk
                && member.IsActive),
        ];
        return clerks[index / branches.Count % clerks.Length];
    }

    private Staff ManagerOf(Branch branch) =>
        staff.First(member => member.HomeBranch == branch.Code && member.Role == StaffRole.BranchManager);
}
