using System.Runtime.CompilerServices;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Configurations;
using ApexPost.Infrastructure.Persistence.Mappers;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories;

/// <summary>
/// Builds a whole transaction with a fixed number of queries. The lines come from three tables, so they are split off
/// from the header: joined, every line would repeat the header and payment columns.
/// </summary>
internal static class TransactionLoader
{
    private const string LinesField = "_lines";
    private const string VerificationsField = "_verifications";

    public static async Task<CounterTransaction?> LoadAsync(
        ApexPostDbContext database,
        IQueryable<CounterTransaction> transactions,
        Guid id,
        CancellationToken cancellationToken)
    {
        var transaction = await transactions
            .Include(item => EF.Property<IEnumerable<TransactionLine>>(item, LinesField)
                .OrderBy(line => EF.Property<DateTime>(line, ColumnRules.CreatedAt))
                .ThenBy(line => line.Id))
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (transaction is null)
        {
            return null;
        }

        var checks = await database.Set<IdentityVerificationRecord>()
            .AsNoTracking()
            .Where(check => check.TransactionId == id)
            .OrderBy(check => check.VerifiedAt)
            .ThenBy(check => EF.Property<DateTime>(check, ColumnRules.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Verifications(transaction).AddRange(checks.Select(IdentityVerificationMapper.ToDomain));
        return transaction;
    }

    /// <summary>
    /// The domain offers no way to add a check without its rules, and a loaded check must skip them. Reaching the list
    /// from here keeps that door out of the public model.
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = VerificationsField)]
    private static extern ref List<IdentityVerification> Verifications(CounterTransaction transaction);
}
