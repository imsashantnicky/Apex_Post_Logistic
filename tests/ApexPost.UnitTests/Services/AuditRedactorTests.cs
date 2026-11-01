using ApexPost.Application.Services;

namespace ApexPost.UnitTests.Services;

public sealed class AuditRedactorTests
{
    private const string Redacted = AuditRedactor.Redacted;

    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("newPassword")]
    [InlineData("DocumentNumber")]
    [InlineData("document_number")]
    [InlineData("apiKey")]
    [InlineData("X-Api-Key")]
    [InlineData("cardNumber")]
    [InlineData("cvv")]
    [InlineData("clientSecret")]
    [InlineData("accessToken")]
    public void Value_under_a_sensitive_name_is_dropped_whole(string name) =>
        Assert.Equal(Redacted, Clean(name, "harmless-looking"));

    [Theory]
    [InlineData("AB1234567", "****4567")]
    [InlineData("ab1234567", "****4567")]
    [InlineData("/x/PASS12345/y", "/x/****2345/y")]
    [InlineData("123456", "****3456")]
    public void Document_like_text_keeps_only_its_last_four_characters(string value, string expected) =>
        Assert.Equal(expected, Clean("id", value));

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    public void Card_like_numbers_are_removed(string value) => Assert.Equal(Redacted, Clean("note", value));

    [Theory]
    [InlineData("3f2504e0-4f89-41d3-9a0c-0305e82c3301")]
    [InlineData("BR001")]
    [InlineData("PKG-0001")]
    [InlineData("Card")]
    [InlineData("Refunded")]
    [InlineData("42")]
    public void Ordinary_values_stay_readable(string value) => Assert.Equal(value, Clean("id", value));

    private static string Clean(string name, string value) =>
        AuditRedactor.Redact([KeyValuePair.Create(name, value)])[name];
}
