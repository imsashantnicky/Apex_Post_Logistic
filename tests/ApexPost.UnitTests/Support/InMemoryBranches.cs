using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;

namespace ApexPost.UnitTests.Support;

/// <summary>The branch ports on top of the in-memory store.</summary>
internal sealed class InMemoryBranches(InMemoryRepository<Branch, BranchCode> store, FakeUnitOfWork unitOfWork)
    : IBranchRepository, IBranchQueries
{
    public Task<Branch?> GetAsync(BranchCode code, CancellationToken cancellationToken) =>
        store.GetAsync(code, cancellationToken);

    public void Update(Branch branch, int expectedVersion) =>
        unitOfWork.Stage(token => store.UpdateAsync(branch, expectedVersion, token));

    public async Task<IReadOnlyList<BranchSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var all = await store.ListAsync(cancellationToken);
        return [.. all.OrderBy(branch => branch.Code.Value, StringComparer.Ordinal).Select(BranchSummary.From)];
    }

    public async Task<BranchSummary?> FindAsync(BranchCode code, CancellationToken cancellationToken) =>
        await store.GetAsync(code, cancellationToken) is { } branch ? BranchSummary.From(branch) : null;
}
