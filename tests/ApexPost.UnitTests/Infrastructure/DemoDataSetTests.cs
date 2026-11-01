using ApexPost.Application.Settings;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Seeding;
using ApexPost.UnitTests.Support;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class DemoDataSetTests
{
    private static readonly DemoDataSet Data =
        DemoDataSet.Create(TestData.Now, TestData.CreateRules(), TestData.Multipliers);

    [Fact]
    public void Branches_match_the_specification()
    {
        (string Code, string Name, bool IsActive)[] expected =
        [
            ("BR001", "Central Counter", true),
            ("BR002", "Harbour Point", true),
            ("BR003", "Airport Terminal", true),
            ("BR004", "Northgate", true),
            ("BR005", "Westfield", true),
            ("BR006", "Old Quay", false),
        ];

        Assert.Equal(expected, Data.Branches.Select(branch => (branch.Code.Value, branch.Name, branch.IsActive)));
    }

    [Fact]
    public void Staff_are_26_across_all_four_roles()
    {
        Assert.Equal(26, Data.Staff.Count);
        Assert.Equal(Enum.GetValues<StaffRole>().Length, Data.Staff.Select(member => member.Role).Distinct().Count());
        Assert.Equal(26, Data.Staff.Select(member => member.Email).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("clerk1.br001@apexpost.test", StaffRole.CounterClerk, "BR001", true)]
    [InlineData("manager.br001@apexpost.test", StaffRole.BranchManager, "BR001", true)]
    [InlineData("auditor1@apexpost.test", StaffRole.Auditor, "BR001", true)]
    [InlineData("admin1@apexpost.test", StaffRole.Administrator, "BR001", true)]
    [InlineData("clerk4.br002@apexpost.test", StaffRole.CounterClerk, "BR002", false)]
    [InlineData("manager.br006@apexpost.test", StaffRole.BranchManager, "BR006", true)]
    [InlineData("clerk1.br005@apexpost.test", StaffRole.CounterClerk, "BR005", true)]
    public void Demo_accounts_exist(string email, StaffRole role, string branch, bool isActive)
    {
        var member = Assert.Single(Data.Staff, candidate => candidate.Email == email);

        Assert.Equal(role, member.Role);
        Assert.Equal(branch, member.HomeBranch.Value);
        Assert.Equal(isActive, member.IsActive);
    }

    [Fact]
    public void Products_are_32_with_all_prefixes_and_tax_categories()
    {
        Assert.Equal(32, Data.Products.Count);
        Assert.Equal(
            ["OFF", "PKG", "STA"],
            Data.Products.Select(product => product.Sku.Value[..3]).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            Enum.GetValues<TaxCategory>().Length,
            Data.Products.Select(product => product.TaxCategory).Distinct().Count());
    }

    [Fact]
    public void Zones_are_Z01_to_Z10_starting_with_local()
    {
        Assert.Equal(
            Enumerable.Range(1, 10).Select(number => $"Z{number:00}"),
            Data.Zones.Select(zone => zone.Code));
        Assert.Equal("Local", Data.Zones[0].Name);
    }

    [Fact]
    public void Every_branch_stocks_every_product_and_nothing_is_negative()
    {
        Assert.Equal(Data.Branches.Count * Data.Products.Count, Data.Stock.Count);
        Assert.All(Data.Stock, item => Assert.True(item.Quantity >= 0));
    }

    [Fact]
    public void Some_stock_is_low_so_the_report_has_rows()
    {
        var threshold = new StockSettings().LowStockThreshold;

        Assert.Contains(Data.Stock, item => item.Quantity <= threshold);
    }

    [Fact]
    public void History_has_48_transactions_over_the_last_14_days()
    {
        var today = DateOnly.FromDateTime(TestData.Now.UtcDateTime);
        var days = Data.Transactions.Select(item => DateOnly.FromDateTime(item.OpenedAt.UtcDateTime)).ToArray();

        Assert.Equal(48, Data.Transactions.Count);
        Assert.Equal(today.AddDays(-14), days.Min());
        Assert.True(days.Max() < today);
        Assert.Equal(14, days.Distinct().Count());
    }

    [Fact]
    public void History_covers_every_kind_of_line_and_outcome()
    {
        ILineItem[] lines = [.. Data.Transactions.SelectMany(transaction => transaction.Lines)];
        var statuses = Data.Transactions.Select(transaction => transaction.Status).ToHashSet();
        HashSet<TransactionStatus> expected =
            [TransactionStatus.Paid, TransactionStatus.Voided, TransactionStatus.Refunded];

        Assert.Contains(lines, line => line is RetailLineItem);
        Assert.Contains(lines, line => line is ShippingLineItem);
        Assert.Contains(lines, line => line is MoneyOrderLineItem);
        Assert.Superset(expected, statuses);
    }

    [Fact]
    public void No_transaction_happened_at_the_closed_branch()
    {
        Assert.DoesNotContain(Data.Transactions, item => item.Branch.Value == DemoBranches.ClosedBranch);
    }

    [Fact]
    public void Verifications_cover_passed_and_failed_and_are_masked()
    {
        IdentityVerification[] checks = [.. Data.Transactions.SelectMany(item => item.Verifications)];

        Assert.Contains(checks, check => check.Result == VerificationResult.Passed);
        Assert.Contains(checks, check => check.Result == VerificationResult.Failed);
        Assert.All(checks, check => Assert.StartsWith("****", check.MaskedNumber, StringComparison.Ordinal));
    }
}
