using ApexPost.Application.Abstractions;

namespace ApexPost.UnitTests.Support;

/// <summary>A readable, fast stand-in for PBKDF2 that counts how often a hash is checked.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    private const string Prefix = "hashed:";

    public int VerifyCount { get; private set; }

    public string Hash(string password) => Prefix + password;

    public bool Verify(string password, string passwordHash)
    {
        VerifyCount++;
        return passwordHash == Prefix + password;
    }
}
