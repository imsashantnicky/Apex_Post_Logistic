using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Services;

/// <summary>
/// Branch administration: open, close, add and remove counters. Every change names the version it saw.
/// </summary>
public sealed class BranchService(
    ICurrentUser user,
    IBranchRepository branches,
    IBranchQueries queries,
    ICounterTransactionRepository transactions,
    IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BranchSummary>> ListAsync(CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator, StaffRole.Auditor);
        return queries.ListAsync(cancellationToken);
    }

    /// <summary>One branch the user may see, for example the counters of the user's own branch.</summary>
    public async Task<BranchSummary> GetAsync(BranchCode code, CancellationToken cancellationToken)
    {
        user.RequireBranch(code);
        return await queries.FindAsync(code, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Branch {code} does not exist.");
    }

    public Task CloseAsync(BranchCode code, int expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(code, expectedVersion, branch => branch.Close(), cancellationToken);

    public Task ReopenAsync(BranchCode code, int expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(code, expectedVersion, branch => branch.Reopen(), cancellationToken);

    public Task AddCounterAsync(BranchCode code, int expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(code, expectedVersion, branch => branch.AddCounter(Guid.CreateVersion7()), cancellationToken);

    /// <summary>An open basket must first be paid or voided; the database keeps a counter that has history.</summary>
    public async Task RemoveCounterAsync(
        BranchCode code,
        Guid counterId,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        if (await transactions.HasOpenBasketAsync(counterId, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainException("This counter has an open basket. Finish or void it first.");
        }

        await ChangeAsync(code, expectedVersion, branch => branch.RemoveCounter(counterId), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ChangeAsync(
        BranchCode code,
        int expectedVersion,
        Action<Branch> change,
        CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        var branch = await branches.GetAsync(code, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Branch {code} does not exist.");
        change(branch);
        branches.Update(branch, expectedVersion);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
