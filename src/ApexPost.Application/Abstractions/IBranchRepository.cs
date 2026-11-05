using ApexPost.Domain.Branches;

namespace ApexPost.Application.Abstractions;

/// <summary>Branches for writing; nothing is stored until the unit of work commits.</summary>
public interface IBranchRepository
{
    Task<Branch?> GetAsync(BranchCode code, CancellationToken cancellationToken);

    /// <summary>Stages a change that only succeeds if the stored branch still has the expected version.</summary>
    void Update(Branch branch, int expectedVersion);
}
