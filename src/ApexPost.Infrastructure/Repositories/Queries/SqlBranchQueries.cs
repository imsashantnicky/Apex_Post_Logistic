using System.Linq.Expressions;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories.Queries;

public sealed class SqlBranchQueries(IDbContextFactory<ApexPostDbContext> contexts) : IBranchQueries
{
    private static readonly Expression<Func<Branch, BranchSummary>> ToSummary = branch => new BranchSummary(
        branch.Id,
        branch.Name,
        branch.Region,
        branch.OpensAt,
        branch.ClosesAt,
        branch.IsActive,
        branch.Counters
            .OrderBy(counter => counter.Number)
            .Select(counter => new CounterView(counter.Id, counter.Number))
            .ToList(),
        branch.Version);

    public async Task<IReadOnlyList<BranchSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            return await database.Set<Branch>()
                .AsNoTracking()
                .OrderBy(branch => branch.Id)
                .Select(ToSummary)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<BranchSummary?> FindAsync(BranchCode code, CancellationToken cancellationToken)
    {
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            return await database.Set<Branch>()
                .AsNoTracking()
                .Where(branch => branch.Id == code)
                .Select(ToSummary)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
