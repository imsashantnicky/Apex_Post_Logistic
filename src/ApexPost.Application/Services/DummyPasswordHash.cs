using ApexPost.Application.Abstractions;

namespace ApexPost.Application.Services;

/// <summary>
/// The hash an unknown email is checked against. It is made once, because making it per request would slow down
/// only the wrong emails and so reveal them. Register as a singleton.
/// </summary>
public sealed class DummyPasswordHash(IPasswordHasher hasher)
{
    private readonly Lazy<string> _hash = new(() => hasher.Hash(Guid.NewGuid().ToString()));

    public string Value => _hash.Value;
}
