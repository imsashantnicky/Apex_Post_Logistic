using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Records;

/// <summary>One manual stock change, append-only.</summary>
[Index(nameof(Branch), Name = "IX_StockAdjustments_BranchCode")]
[Index(nameof(Sku), Name = "IX_StockAdjustments_Sku")]
[Index(nameof(StaffId), Name = "IX_StockAdjustments_StaffId")]
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
