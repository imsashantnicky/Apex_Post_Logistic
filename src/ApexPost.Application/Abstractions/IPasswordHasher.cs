namespace ApexPost.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Compares in constant time; returns false for a malformed hash.</summary>
    bool Verify(string password, string passwordHash);
}
