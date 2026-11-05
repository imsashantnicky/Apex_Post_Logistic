using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;

namespace ApexPost.Application.Abstractions;

/// <summary>Read-only branch data for the screens, ordered by branch code.</summary>
public interface IBranchQueries
{
    Task<IReadOnlyList<BranchSummary>> ListAsync(CancellationToken cancellationToken);

    Task<BranchSummary?> FindAsync(BranchCode code, CancellationToken cancellationToken);
}
