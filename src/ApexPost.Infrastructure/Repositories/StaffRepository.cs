using ApexPost.Application.Abstractions;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories;

/// <summary>Emails compare by the case-insensitive collation of the column, so an index can answer them.</summary>
public sealed class StaffRepository(ApexPostDbContext database) : IStaffRepository
{
    public async Task<Staff?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await database.Set<Staff>().FindAsync([id], cancellationToken).ConfigureAwait(false);

    public Task<Staff?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        database.Set<Staff>().SingleOrDefaultAsync(member => member.Email == email, cancellationToken);

    public Task<bool> EmailTakenAsync(string email, Guid? ownId, CancellationToken cancellationToken) =>
        database.Set<Staff>()
            .AsNoTracking()
            .AnyAsync(member => member.Email == email && member.Id != ownId, cancellationToken);

    public void Add(Staff member) => database.Add(member);

    public void Update(Staff member, int expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(member);
        ExpectedVersion.Apply(database.Entry(member), expectedVersion);
    }
}
