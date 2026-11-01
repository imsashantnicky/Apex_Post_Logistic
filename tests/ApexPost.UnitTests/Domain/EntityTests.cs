using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Domain;

public sealed class EntityTests
{
    [Fact]
    public void Entities_are_equal_by_type_and_id()
    {
        var id = Guid.NewGuid();
        var staff = CreateStaff();

        Assert.Equal(new Counter(id, 1), new Counter(id, 2));
        Assert.NotEqual(new Counter(id, 1), new Counter(Guid.NewGuid(), 1));
        Assert.False(new Counter(staff.Id, 1).Equals(staff));
    }

    [Fact]
    public void Empty_id_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Counter(Guid.Empty, 1));
    }

    [Fact]
    public void Branch_must_open_before_it_closes()
    {
        var nine = new TimeOnly(9, 0);
        Counter[] counters = [new(Guid.NewGuid(), 1)];

        Assert.Throws<DomainException>(() =>
            new Branch(new BranchCode(HomeBranch), "Central", "North", nine, nine, counters));
        Assert.Throws<DomainException>(() =>
            new Branch(new BranchCode(HomeBranch), "Central", "North", nine, nine.AddMinutes(-1), counters));
        Assert.Equal(
            nine.AddMinutes(1),
            new Branch(new BranchCode(HomeBranch), "Central", "North", nine, nine.AddMinutes(1), counters).ClosesAt);
    }

    [Fact]
    public void Branch_needs_at_least_one_unique_counter()
    {
        var code = new BranchCode(HomeBranch);
        var open = new TimeOnly(8, 0);
        var close = new TimeOnly(18, 0);

        Assert.Throws<DomainException>(() => new Branch(code, "Central", "North", open, close, []));
        Assert.Throws<DomainException>(() => new Branch(
            code, "Central", "North", open, close, [new Counter(Guid.NewGuid(), 1), new Counter(Guid.NewGuid(), 1)]));
    }

    [Fact]
    public void New_counter_gets_the_next_number()
    {
        var branch = CreateBranch();

        var counter = branch.AddCounter(Guid.NewGuid());

        Assert.Equal(2, counter.Number);
        Assert.Equal(2, branch.Counters.Count);
        Assert.Throws<DomainException>(() => branch.AddCounter(counter.Id));
    }

    [Fact]
    public void Removing_a_counter_keeps_the_others_and_frees_its_number()
    {
        var branch = CreateBranch();
        var first = branch.Counters[0];
        var second = branch.AddCounter(Guid.NewGuid());

        branch.RemoveCounter(second.Id);

        Assert.Equal(first, Assert.Single(branch.Counters));
        Assert.Equal(2, branch.AddCounter(Guid.NewGuid()).Number);
    }

    [Fact]
    public void Last_counter_and_unknown_counters_cannot_be_removed()
    {
        var branch = CreateBranch();

        Assert.Throws<DomainException>(() => branch.RemoveCounter(branch.Counters[0].Id));
        Assert.Throws<DomainException>(() => branch.RemoveCounter(Guid.NewGuid()));
        Assert.Single(branch.Counters);
    }

    [Fact]
    public void Staff_email_is_trimmed_and_validated_on_every_change()
    {
        var staff = CreateStaff();

        staff.Email = "  kim.lee@apexpost.test ";

        Assert.Equal("kim.lee@apexpost.test", staff.Email);
        Assert.Throws<DomainException>(() => staff.Email = "kim.lee");
        Assert.Equal("kim.lee@apexpost.test", staff.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sam")]
    [InlineData("sam@")]
    [InlineData("@apexpost.test")]
    [InlineData("sam@apexpost")]
    [InlineData("sam@@apexpost.test")]
    [InlineData("sam lee@apexpost.test")]
    [InlineData("sam@.apexpost")]
    [InlineData("sam@apexpost.")]
    public void Invalid_staff_email_is_rejected(string email)
    {
        Assert.Throws<DomainException>(() =>
            new Staff(Guid.NewGuid(), "Sam Taylor", email, StaffRole.CounterClerk, new BranchCode(HomeBranch)));
    }

    [Fact]
    public void Staff_name_and_role_are_validated()
    {
        var staff = CreateStaff();

        Assert.Throws<DomainException>(() => staff.Name = " ");
        Assert.Throws<DomainException>(() => staff.Name = new string('a', 101));
        Assert.Throws<DomainException>(() => staff.Role = (StaffRole)99);
        staff.Role = StaffRole.Auditor;
        Assert.Equal(StaffRole.Auditor, staff.Role);
    }

    [Fact]
    public void Staff_can_be_deactivated_and_activated()
    {
        var staff = CreateStaff();

        staff.Deactivate();
        Assert.False(staff.IsActive);
        staff.Activate();
        Assert.True(staff.IsActive);
    }

    [Fact]
    public void Product_price_must_be_above_zero()
    {
        Assert.Equal(new Money(0.01m), CreateProduct(price: 0.01m).UnitPrice);
        Assert.Throws<DomainException>(() => CreateProduct(price: 0m));
    }

    [Fact]
    public void Product_category_and_name_are_validated()
    {
        var sku = new Sku(FirstSku);
        var price = new Money(1m);

        Assert.Throws<DomainException>(() =>
            new Product(sku, " ", ProductCategory.Packaging, price, TaxCategory.Standard));
        Assert.Throws<DomainException>(() =>
            new Product(sku, "Box", (ProductCategory)99, price, TaxCategory.Standard));
        Assert.Throws<DomainException>(() =>
            new Product(sku, "Box", ProductCategory.Packaging, price, (TaxCategory)99));
    }

    [Fact]
    public void Zone_code_is_upper_cased_and_rates_are_positive()
    {
        Assert.Equal("Z09", new ShippingZone(" z09 ", "Islands", new Money(5m), new Money(1m)).Code);
        Assert.Throws<DomainException>(() => CreateZone(baseRate: 0m));
        Assert.Throws<DomainException>(() => CreateZone(perKgRate: 0m));
        Assert.Throws<DomainException>(() =>
            new ShippingZone("Z01", "Domestic", new Money(5m), new Money(1m, "EUR")));
    }
}
