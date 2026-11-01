using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Abstractions;

/// <summary>Staff for writing and for signing in; nothing is stored until the unit of work commits.</summary>
public interface IStaffRepository
{
    Task<Staff?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Staff?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>True when someone other than <paramref name="ownId"/> already uses the email.</summary>
    Task<bool> EmailTakenAsync(string email, Guid? ownId, CancellationToken cancellationToken);

    void Add(Staff member);

    /// <summary>Stages a change that only succeeds if the stored member still has the expected version.</summary>
    void Update(Staff member, int expectedVersion);
}
