using ApexPost.Domain.Common;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Seeding;
using ApexPost.Infrastructure.Seeding;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class DevelopmentHistoryTests
{
    private const string ClosedBranch = DemoBranches.ClosedBranch;

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 15, 30, 0, TimeSpan.Zero);

    private static readonly PaymentRules Rules = new(
        new Money(300m),
        TaxRule.From(new TaxRates(20m, 5m, 0m)),
        LowStockRule.AtOrBelow(5));

    [Fact]
    public void History_has_at_least_200_transactions_within_60_days_and_none_in_the_future()
    {
        var history = Create(Now);

        Assert.True(history.Count >= 200);
        Assert.All(history, transaction => Assert.True(transaction.OpenedAt <= Now));
        var oldest = Now.AddDays(-DevelopmentHistory.DayCount - 1);
        Assert.All(history, transaction => Assert.True(transaction.OpenedAt >= oldest));
        Assert.All(
            history.Where(transaction => transaction.Reversal is not null),
            transaction => Assert.True(transaction.Reversal!.ReversedAt <= Now));
    }

    [Fact]
    public void History_mixes_all_line_types_and_all_final_statuses()
    {
        var history = Create(Now);
        ILineItem[] lines = [.. history.SelectMany(transaction => transaction.Lines)];

        Assert.Contains(lines, line => line is RetailLineItem);
        Assert.Contains(lines, line => line is ShippingLineItem);
        Assert.Contains(lines, line => line is MoneyOrderLineItem);
        Assert.Contains(history, transaction => transaction.Status == TransactionStatus.Paid);
        Assert.Contains(history, transaction => transaction.Status == TransactionStatus.Voided);
        Assert.Contains(history, transaction => transaction.Status == TransactionStatus.Refunded);
    }

    [Fact]
    public void Money_orders_above_the_threshold_have_a_passed_masked_verification()
    {
        CounterTransaction[] checkedBaskets =
        [
            .. Create(Now).Where(transaction => transaction.RequiresVerification(Rules.VerificationThreshold)),
        ];

        Assert.NotEmpty(checkedBaskets);
        Assert.All(checkedBaskets, transaction =>
        {
            Assert.True(transaction.HasPassedVerification);
            Assert.All(
                transaction.Verifications,
                check => Assert.StartsWith("****", check.MaskedNumber, StringComparison.Ordinal));
        });
    }

    [Theory]
    [InlineData(15, 30)]
    [InlineData(0, 5)]
    public void Two_branches_have_no_sales_today_and_the_others_do(int hour, int minute)
    {
        var now = new DateTimeOffset(2026, 11, 1, hour, minute, 0, TimeSpan.Zero);

        string[] sellingToday =
        [
            .. Create(now)
                .Where(transaction => transaction.OpenedAt.UtcDateTime.Date == now.UtcDateTime.Date)
                .Select(transaction => transaction.Branch.Value)
                .Distinct(),
        ];

        Assert.Equal(4, sellingToday.Length);
        Assert.DoesNotContain(DevelopmentHistory.NoSalesTodayBranch, sellingToday);
        Assert.DoesNotContain(ClosedBranch, sellingToday);
    }

    [Fact]
    public void The_closed_branch_never_sells()
    {
        Assert.DoesNotContain(Create(Now), transaction => transaction.Branch.Value == ClosedBranch);
    }

    [Fact]
    public void The_same_clock_gives_the_same_history()
    {
        Assert.Equal(Fingerprint(Create(Now)), Fingerprint(Create(Now)));
    }

    private static IReadOnlyList<CounterTransaction> Create(DateTimeOffset now)
    {
        var branches = DemoBranches.Create();
        var products = DemoProducts.Create();
        return new DevelopmentHistory(
                branches,
                DemoStaff.Create(),
                products,
                DemoZones.Create(),
                DemoStock.Create(branches, products),
                Rules,
                new ServiceLevelMultipliers(1.0m, 1.6m, 1.3m))
            .Create(now);
    }

    private static string[] Fingerprint(IEnumerable<CounterTransaction> history) =>
    [
        .. history.Select(transaction =>
            $"{transaction.Branch} {transaction.OpenedAt:O} {transaction.Status} {transaction.Total} "
            + string.Join(',', transaction.Lines.Select(line => line.Description))),
    ];
}
