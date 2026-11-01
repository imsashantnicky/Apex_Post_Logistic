using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories;

/// <summary>
/// Tracked stock rows carry their RowVersion. An UPDATE only finds a row that still has the version it was read with,
/// so of two sales of the last unit on different servers the second one fails instead of overselling.
/// </summary>
public sealed class StockRepository(ApexPostDbContext database) : IStockRepository
{
    private const string Quantity = nameof(StockItem.Quantity);

    public async Task<IReadOnlyList<StockItem>> GetManyAsync(
        BranchCode branch,
        IEnumerable<Sku> skus,
        CancellationToken cancellationToken)
    {
        Sku[] wanted = [.. skus.Distinct()];
        return await database.Set<StockItem>()
            .Where(item => item.Branch == branch && wanted.Contains(item.Sku))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(StockItem item) => database.Add(item);

    /// <summary>Forces the row into the UPDATE, so the RowVersion is checked even for an equal quantity.</summary>
    public void Update(IEnumerable<StockItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            database.Entry(item).Property(Quantity).IsModified = true;
        }
    }

    public void AddAdjustment(StockAdjustment adjustment, Guid staffId, DateTimeOffset at) =>
        database.Add(StockAdjustmentMapper.ToRecord(adjustment, staffId, at));
}
