using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// A manual stock correction as one atomic step. Unlike a repository this commits by itself, the quantity and its
/// history together, so it needs no unit of work. A first delivery creates the stock; too little stock throws
/// InsufficientStockException.
/// </summary>
public interface IStockAdjuster
{
    /// <summary>Applies the change and returns the new quantity.</summary>
    Task<int> AdjustAsync(
        BranchCode branch,
        Sku sku,
        int change,
        string reason,
        Guid staffId,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
