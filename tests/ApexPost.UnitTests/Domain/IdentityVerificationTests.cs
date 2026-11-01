using ApexPost.Domain.Common;
using ApexPost.Domain.Verification;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Domain;

public sealed class IdentityVerificationTests
{
    [Fact]
    public void Only_the_last_four_characters_are_kept()
    {
        var verification = CreateVerification(VerificationResult.Passed);

        Assert.Equal("****5678", verification.MaskedNumber);
        Assert.DoesNotContain(DocumentNumber, verification.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Shortest_document_number_has_six_characters()
    {
        Assert.Equal("****3456", IdentityVerification.Mask("123456"));
        var error = Assert.Throws<DomainException>(() => IdentityVerification.Mask("12345"));
        Assert.DoesNotContain("12345", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1234-5678")]
    [InlineData("   ")]
    public void Document_number_must_be_letters_and_digits(string number)
    {
        Assert.Throws<DomainException>(() => IdentityVerification.Mask(number));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("****56")]
    [InlineData("xxxx5678")]
    public void Unmasked_number_cannot_be_stored(string number)
    {
        Assert.Throws<DomainException>(() =>
            new IdentityVerification(DocumentType.Passport, number, VerificationResult.Passed, Now));
    }

    [Theory]
    [InlineData(VerificationResult.Passed, true)]
    [InlineData(VerificationResult.Failed, false)]
    [InlineData(VerificationResult.Pending, false)]
    public void Only_a_passed_check_counts(VerificationResult result, bool expected)
    {
        Assert.Equal(expected, CreateVerification(result).IsPassed);
    }

    [Fact]
    public void Undefined_document_type_is_rejected()
    {
        Assert.Throws<DomainException>(() =>
            IdentityVerification.FromDocument((DocumentType)99, DocumentNumber, VerificationResult.Passed, Now));
    }
}
