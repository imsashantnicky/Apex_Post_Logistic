using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Security;

/// <summary>Password hashes in a shadow column of Staff, which no staff query selects.</summary>
public sealed class SqlCredentialStore(ApexPostDbContext database) : ICredentialStore
{
    public Task<string?> GetHashAsync(Guid staffId, CancellationToken cancellationToken) =>
        database.Set<Staff>()
            .AsNoTracking()
            .Where(member => member.Id == staffId)
            .Select(member => EF.Property<string?>(member, StaffConfiguration.PasswordHash))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Stages the hash; it is stored with the rest of the use case by the unit of work.</summary>
    public async Task SetHashAsync(Guid staffId, string passwordHash, CancellationToken cancellationToken)
    {
        var member = await database.Set<Staff>().FindAsync([staffId], cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Staff member {staffId} does not exist.");
        database.Entry(member).Property(StaffConfiguration.PasswordHash).CurrentValue = passwordHash;
    }
}
