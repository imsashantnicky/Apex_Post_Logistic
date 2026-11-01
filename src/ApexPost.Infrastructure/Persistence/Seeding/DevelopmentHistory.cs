using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Seeding;

namespace ApexPost.Infrastructure.Persistence.Seeding;

/// <summary>
/// 240 transactions over the 60 days before today plus 12 from today, built through the domain methods, so every
/// business rule holds and the sold goods leave the stock. Random(42) fixes the mix; ids and dates follow the clock.
/// Today's sales leave out BR005 and the closed BR006, so those two branches show an empty day.
/// </summary>
public sealed class DevelopmentHistory(
    IReadOnlyList<Branch> branches,
    IReadOnlyList<Staff> staff,
    IReadOnlyList<Product> products,
    IReadOnlyList<ShippingZone> zones,
    IReadOnlyCollection<StockItem> stock,
    PaymentRules rules,
    ServiceLevelMultipliers multipliers)
{
    public const int PastCount = 240;
    public const int TodayCount = 12;
    public const int DayCount = 60;
    public const string NoSalesTodayBranch = "BR005";

    private const int RandomSeed = 42;
    private const int Kinds = 7;
    private const int PercentRange = 100;
    private const int AbandonedBelow = 2;
    private const int VoidedBelow = 9;
    private const int RefundedBelow = 15;
    private const int FailedCheckOneIn = 6;
    private const int MinutesToPay = 4;
    private const int MinutesToVoid = 40;
    private const int MinutesToRefund = 50;
    private const int LastCustomerMargin = 60;
    private const int MaxQuantity = 3;
    private const int DocumentNumbers = 100_000;
    private const string VoidReason = "Customer changed their mind at the counter.";
    private const string RefundReason = "Damaged item returned with receipt.";
    private const string AbandonedReason = "Basket abandoned; customer left.";

    private static readonly decimal[] SmallOrders = [50m, 120m, 200m, 250m];
    private static readonly decimal[] LargeOrders = [450m, 800m, 1250m];
    private static readonly ServiceLevel[] Levels =
        [ServiceLevel.Standard, ServiceLevel.Express, ServiceLevel.Registered];
    private static readonly DocumentType[] Documents =
        [DocumentType.Passport, DocumentType.NationalIdCard, DocumentType.DrivingLicence];

    private readonly Random _random = new(RandomSeed);
    private readonly Product[] _popular = [.. products.Where((_, index) => !DemoStock.IsKeptLow(index))];

    public IReadOnlyList<CounterTransaction> Create(DateTimeOffset now)
    {
        Branch[] open = [.. branches.Where(branch => branch.IsActive)];
        Branch[] openToday = [.. open.Where(branch => branch.Code.Value != NoSalesTodayBranch)];
        List<CounterTransaction> history = [];
        for (var index = 0; index < PastCount; index++)
        {
            history.Add(Past(Pick(open), now));
        }

        for (var index = 0; index < TodayCount; index++)
        {
            history.Add(Today(openToday[index % openToday.Length], index, now));
        }

        return history;
    }

    private CounterTransaction Past(Branch branch, DateTimeOffset now)
    {
        var openedAt = OpenedAt(branch, _random.Next(1, DayCount + 1), now);
        var clerk = ClerkOf(branch);
        var transaction = Basket(branch, clerk, openedAt);
        var outcome = _random.Next(PercentRange);
        if (outcome < AbandonedBelow)
        {
            transaction.Void(ManagerOf(branch), AbandonedReason, stock, openedAt.AddMinutes(MinutesToVoid));
            return transaction;
        }

        var paidAt = openedAt.AddMinutes(MinutesToPay);
        transaction.Pay(clerk, Pick(Enum.GetValues<PaymentMethod>()), rules, stock, paidAt);
        if (outcome < VoidedBelow)
        {
            transaction.Void(ManagerOf(branch), VoidReason, stock, openedAt.AddMinutes(MinutesToVoid));
        }
        else if (outcome < RefundedBelow)
        {
            transaction.Refund(ManagerOf(branch), RefundReason, stock, openedAt.AddMinutes(MinutesToRefund));
        }

        return transaction;
    }

    /// <summary>Spreads the sales over the part of today that has passed, so none lies in the future.</summary>
    private CounterTransaction Today(Branch branch, int index, DateTimeOffset now)
    {
        var midnight = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var elapsed = now - midnight;
        var openedAt = midnight + (elapsed * index / (2 * TodayCount));
        var clerk = ClerkOf(branch);
        var transaction = Basket(branch, clerk, openedAt);
        transaction.Pay(clerk, Pick(Enum.GetValues<PaymentMethod>()), rules, stock, openedAt + (elapsed / 4));
        return transaction;
    }

    private CounterTransaction Basket(Branch branch, Staff clerk, DateTimeOffset openedAt)
    {
        var transaction = CounterTransaction.Open(branch, Pick(branch.Counters), clerk, openedAt);
        foreach (var line in Lines())
        {
            transaction.AddLine(clerk, line);
        }

        Verify(transaction, clerk, openedAt);
        return transaction;
    }

    private ILineItem[] Lines() => _random.Next(Kinds) switch
    {
        0 or 1 => [Retail(), Retail()],
        2 => [Parcel(), Retail()],
        3 => [Letter()],
        4 => [MoneyOrder(Pick(SmallOrders))],
        5 => [MoneyOrder(Pick(LargeOrders))],
        _ => [Retail(), Parcel(), MoneyOrder(Pick(SmallOrders))],
    };

    private void Verify(CounterTransaction transaction, Staff clerk, DateTimeOffset openedAt)
    {
        if (!transaction.RequiresVerification(rules.VerificationThreshold))
        {
            return;
        }

        var document = Pick(Documents);
        if (_random.Next(FailedCheckOneIn) == 0)
        {
            transaction.RecordVerification(clerk, Check(document, VerificationResult.Failed, openedAt));
        }

        transaction.RecordVerification(clerk, Check(document, VerificationResult.Passed, openedAt));
    }

    private IdentityVerification Check(DocumentType document, VerificationResult result, DateTimeOffset at) =>
        IdentityVerification.FromDocument(
            document,
            DemoHistory.DocumentNumber(_random.Next(DocumentNumbers)),
            result,
            at);

    private RetailLineItem Retail() => RetailLineItem.For(Pick(_popular), _random.Next(1, MaxQuantity + 1));

    private ShippingLineItem Parcel()
    {
        var shipment = new Shipment(
            ShipmentType.Parcel,
            Pick(Levels),
            Weight.FromKilograms(0.5m + (_random.Next(19) * 0.5m)),
            new Dimensions(20m + _random.Next(10), 15m, 10m),
            DemoHistory.Recipient(_random.Next(int.MaxValue)));
        return ShippingLineItem.Create(shipment, Pick(zones), multipliers);
    }

    private ShippingLineItem Letter()
    {
        var shipment = new Shipment(
            ShipmentType.Letter,
            ServiceLevel.Standard,
            new Weight(20 + _random.Next(480)),
            new Dimensions(24m, 16m, 0.5m),
            DemoHistory.Recipient(_random.Next(int.MaxValue)));
        return ShippingLineItem.Create(shipment, Pick(zones), multipliers);
    }

    private MoneyOrderLineItem MoneyOrder(decimal amount) =>
        MoneyOrderLineItem.Issue(
            new Money(amount),
            Pick(staff).Name,
            DemoHistory.Recipient(_random.Next(int.MaxValue)).Name);

    private DateTimeOffset OpenedAt(Branch branch, int daysAgo, DateTimeOffset now)
    {
        var day = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-daysAgo);
        var openMinutes = (int)(branch.ClosesAt - branch.OpensAt).TotalMinutes;
        var time = branch.OpensAt.AddMinutes(_random.Next(openMinutes - LastCustomerMargin));
        return new DateTimeOffset(day.ToDateTime(time), TimeSpan.Zero);
    }

    private Staff ClerkOf(Branch branch) =>
        Pick(
        [
            .. staff.Where(member => member.HomeBranch == branch.Code
                && member.Role == StaffRole.CounterClerk
                && member.IsActive),
        ]);

    private Staff ManagerOf(Branch branch) =>
        staff.First(member => member.HomeBranch == branch.Code && member.Role == StaffRole.BranchManager);

    private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];
}
