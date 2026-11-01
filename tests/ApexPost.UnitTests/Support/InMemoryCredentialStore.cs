using ApexPost.Application.Abstractions;

namespace ApexPost.UnitTests.Support;

internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<Guid, string> _hashes = [];

    public Task<string?> GetHashAsync(Guid staffId, CancellationToken cancellationToken) =>
        Task.FromResult(_hashes.GetValueOrDefault(staffId));

    public Task SetHashAsync(Guid staffId, string passwordHash, CancellationToken cancellationToken)
    {
        _hashes[staffId] = passwordHash;
        return Task.CompletedTask;
    }
}
