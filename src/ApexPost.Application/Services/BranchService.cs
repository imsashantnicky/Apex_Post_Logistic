using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Services;

/// <summary>Branch administration: open, close and add counters. Every change names the version it saw.</summary>
public sealed class BranchService(ICurrentUser user, IRepository<Branch, BranchCode> branches)
{
    public async Task<IReadOnlyList<BranchSummary>> ListAsync(CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator, StaffRole.Auditor);
        var all = await branches.ListAsync(cancellationToken).ConfigureAwait(false);
        return [.. all.OrderBy(branch => branch.Code.Value, StringComparer.Ordinal).Select(BranchSummary.From)];
    }

    /// <summary>One branch the user may see, for example the counters of the user's own branch.</summary>
    public async Task<BranchSummary> GetAsync(BranchCode code, CancellationToken cancellationToken)
    {
        user.RequireBranch(code);
        return BranchSummary.From(await LoadAsync(code, cancellationToken).ConfigureAwait(false));
    }

    public Task CloseAsync(BranchCode code, int expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(code, expectedVersion, branch => branch.Close(), cancellationToken);

    public Task ReopenAsync(BranchCode code, int expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(code, expectedVersion, branch => branch.Reopen(), cancellationToken);

    public Task AddCounterAsync(BranchCode code, int expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(code, expectedVersion, branch => branch.AddCounter(Guid.CreateVersion7()), cancellationToken);

    private async Task ChangeAsync(
        BranchCode code,
        int expectedVersion,
        Action<Branch> change,
        CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        var branch = await LoadAsync(code, cancellationToken).ConfigureAwait(false);
        change(branch);
        await branches.UpdateAsync(branch, expectedVersion, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Branch> LoadAsync(BranchCode code, CancellationToken cancellationToken) =>
        await branches.GetAsync(code, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Branch {code} does not exist.");
}
