using System.Linq.Expressions;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories.Queries;

public sealed class SqlStaffQueries(IDbContextFactory<ApexPostDbContext> contexts) : IStaffQueries
{
    private static readonly Expression<Func<Staff, StaffSummary>> ToSummary = member => new StaffSummary(
        member.Id,
        member.Name,
        member.Email,
        member.Role,
        member.HomeBranch,
        member.IsActive,
        member.Version);

    public async Task<IReadOnlyList<StaffSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            return await database.Set<Staff>()
                .AsNoTracking()
                .OrderBy(member => member.HomeBranch)
                .ThenBy(member => member.Name)
                .Select(ToSummary)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<StaffSummary?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (database.ConfigureAwait(false))
        {
            return await database.Set<Staff>()
                .AsNoTracking()
                .Where(member => member.Id == id)
                .Select(ToSummary)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
