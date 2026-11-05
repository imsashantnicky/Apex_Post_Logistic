namespace ApexPost.Application.Abstractions;

/// <summary>
/// Keeps password hashes away from the staff queries, so a staff list never carries a hash. Setting a hash may only
/// stage it, so that a new member and the password are committed together.
/// </summary>
public interface ICredentialStore
{
    Task<string?> GetHashAsync(Guid staffId, CancellationToken cancellationToken);

    Task SetHashAsync(Guid staffId, string passwordHash, CancellationToken cancellationToken);
}
