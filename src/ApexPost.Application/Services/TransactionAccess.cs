using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Services;

/// <summary>
/// Loads transactions for the current user and checks role and branch first, so a forbidden request fails
/// with <see cref="ForbiddenException"/> before any business rule runs.
/// </summary>
public sealed class TransactionAccess(
    ICurrentUser user,
    IRepository<Staff, Guid> staff,
    IRepository<CounterTransaction, Guid> transactions)
{
    public ICurrentUser User => user;

    public async Task<Staff> CurrentStaffAsync(CancellationToken cancellationToken) =>
        await staff.GetAsync(user.StaffId, cancellationToken).ConfigureAwait(false)
            ?? throw new ForbiddenException("The signed-in account no longer exists.");

    /// <summary>Loads a transaction the user may see.</summary>
    public async Task<CounterTransaction> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await transactions.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Transaction {id} does not exist.");
        user.RequireBranch(transaction.Branch);
        return transaction;
    }

    /// <summary>Loads a transaction for counter work by a clerk or manager of the same branch.</summary>
    public Task<(CounterTransaction Transaction, Staff Clerk)> ForCounterWorkAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.CounterClerk, StaffRole.BranchManager);
        return LoadWithStaffAsync(id, cancellationToken);
    }

    /// <summary>Loads a transaction for a void or refund by a Branch Manager of the same branch.</summary>
    public Task<(CounterTransaction Transaction, Staff Manager)> ForReversalAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.BranchManager);
        return LoadWithStaffAsync(id, cancellationToken);
    }

    public Task AddAsync(CounterTransaction transaction, CancellationToken cancellationToken) =>
        transactions.AddAsync(transaction, cancellationToken);

    public Task SaveAsync(CounterTransaction transaction, CancellationToken cancellationToken) =>
        transactions.UpdateAsync(transaction, transaction.Version, cancellationToken);

    private async Task<(CounterTransaction, Staff)> LoadWithStaffAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await ReadAsync(id, cancellationToken).ConfigureAwait(false);
        return (transaction, await CurrentStaffAsync(cancellationToken).ConfigureAwait(false));
    }
}
