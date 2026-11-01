using ApexPost.Application.Abstractions;
using ApexPost.Domain.Branches;
using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories;

public sealed class BranchRepository(ApexPostDbContext database) : IBranchRepository
{
    public Task<Branch?> GetAsync(BranchCode code, CancellationToken cancellationToken) =>
        database.Set<Branch>()
            .Include(branch => branch.Counters)
            .SingleOrDefaultAsync(branch => branch.Id == code, cancellationToken);

    /// <summary>A counter the domain removed is deleted here: the key is Restrict, so EF never cascades.</summary>
    public void Update(Branch branch, int expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(branch);
        foreach (var removed in database.ChangeTracker.Entries<Counter>()
            .Where(entry => !branch.Counters.Contains(entry.Entity)))
        {
            removed.State = EntityState.Deleted;
        }

        ExpectedVersion.Apply(database.Entry(branch), expectedVersion);
    }
}
