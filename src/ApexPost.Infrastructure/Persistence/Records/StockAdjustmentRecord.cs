using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;

namespace ApexPost.Infrastructure.Persistence.Records;

/// <summary>One manual stock change, append-only.</summary>
public sealed class StockAdjustmentRecord
{
    public Guid Id { get; set; }

    public BranchCode Branch { get; set; } = null!;

    public Sku Sku { get; set; } = null!;

    public int Change { get; set; }

    public int NewQuantity { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid StaffId { get; set; }

    public DateTimeOffset AdjustedAt { get; set; }
}
