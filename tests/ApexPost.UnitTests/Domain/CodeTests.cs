using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;

namespace ApexPost.UnitTests.Domain;

public sealed class CodeTests
{
    [Theory]
    [InlineData("BR001", "BR001")]
    [InlineData(" br007 ", "BR007")]
    public void Branch_code_is_BR_and_three_digits(string input, string expected)
    {
        Assert.Equal(expected, new BranchCode(input).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BR01")]
    [InlineData("BR0001")]
    [InlineData("XX001")]
    [InlineData("BRABC")]
    public void Invalid_branch_code_is_rejected(string input)
    {
        Assert.Throws<DomainException>(() => new BranchCode(input));
    }

    [Theory]
    [InlineData("PKG-0001", "PKG-0001")]
    [InlineData("sta-0420", "STA-0420")]
    public void Sku_is_three_letters_dash_four_digits(string input, string expected)
    {
        Assert.Equal(expected, new Sku(input).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PK-0001")]
    [InlineData("PKG0001")]
    [InlineData("PKG-001")]
    [InlineData("PKG-00011")]
    [InlineData("P1G-0001")]
    [InlineData("PKG-00A1")]
    public void Invalid_sku_is_rejected(string input)
    {
        Assert.Throws<DomainException>(() => new Sku(input));
    }

    [Fact]
    public void Codes_are_equal_by_value()
    {
        Assert.Equal(new BranchCode("br001"), new BranchCode("BR001"));
        Assert.Equal("BR001/PKG-0001", new StockKey(new BranchCode("BR001"), new Sku("PKG-0001")).ToString());
    }
}
