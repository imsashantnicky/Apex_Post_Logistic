using ApexPost.Application.Settings;
using ApexPost.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class Pbkdf2PasswordHasherTests
{
    private const string Password = "correct horse battery";
    private const int FastIterations = 1000;
    private const char Separator = '$';
    private const int HashPart = 3;

    private readonly Pbkdf2PasswordHasher _hasher = Hasher(FastIterations);

    [Fact]
    public void Correct_password_verifies()
    {
        Assert.True(_hasher.Verify(Password, _hasher.Hash(Password)));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        Assert.False(_hasher.Verify("wrong password", _hasher.Hash(Password)));
    }

    [Fact]
    public void Every_hash_gets_its_own_salt()
    {
        Assert.NotEqual(_hasher.Hash(Password), _hasher.Hash(Password));
    }

    [Fact]
    public void Stored_text_never_contains_the_password()
    {
        Assert.DoesNotContain(Password, _hasher.Hash(Password), StringComparison.Ordinal);
    }

    [Fact]
    public void Default_work_factor_is_600_000_iterations()
    {
        Assert.Equal(600_000, new PasswordSettings().Iterations);
    }

    [Fact]
    public void Old_hash_still_verifies_after_the_work_factor_changes()
    {
        var oldHash = Hasher(FastIterations).Hash(Password);

        Assert.True(Hasher(FastIterations * 2).Verify(Password, oldHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2-SHA256$1000$c2FsdA==")]
    [InlineData("MD5$1000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2-SHA256$many$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2-SHA256$0$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2-SHA256$1000$###$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2-SHA256$1000$c2FsdA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Damaged_hash_format_returns_false_instead_of_throwing(string damaged)
    {
        Assert.False(_hasher.Verify(Password, damaged));
    }

    [Fact]
    public void Changed_hash_bytes_return_false()
    {
        var parts = _hasher.Hash(Password).Split(Separator);
        var firstCharacter = parts[HashPart][0] == 'A' ? 'B' : 'A';
        parts[HashPart] = firstCharacter + parts[HashPart][1..];

        Assert.False(_hasher.Verify(Password, string.Join(Separator, parts)));
    }

    [Fact]
    public void Non_positive_iterations_are_refused_at_startup()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Hasher(0));
    }

    private static Pbkdf2PasswordHasher Hasher(int iterations) =>
        new(Options.Create(new PasswordSettings { Iterations = iterations }));
}
