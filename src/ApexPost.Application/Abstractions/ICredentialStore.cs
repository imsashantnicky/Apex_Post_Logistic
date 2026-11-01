namespace ApexPost.Application.Abstractions;

/// <summary>Keeps password hashes apart from staff records, so a staff list never carries a hash.</summary>
public interface ICredentialStore
{
    Task<string?> GetHashAsync(Guid staffId, CancellationToken cancellationToken);

    Task SetHashAsync(Guid staffId, string passwordHash, CancellationToken cancellationToken);
}
