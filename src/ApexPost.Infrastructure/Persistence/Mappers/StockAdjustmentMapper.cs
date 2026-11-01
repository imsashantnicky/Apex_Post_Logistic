using ApexPost.Application.DTOs;
using ApexPost.Infrastructure.Persistence.Records;

namespace ApexPost.Infrastructure.Persistence.Mappers;

/// <summary>Maps a stock adjustment to its append-only row and back.</summary>
public static class StockAdjustmentMapper
{
    public static StockAdjustmentRecord ToRecord(StockAdjustment adjustment, Guid staffId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        return new StockAdjustmentRecord
        {
            Id = Guid.CreateVersion7(at),
            Branch = adjustment.Branch,
            Sku = adjustment.Sku,
            Change = adjustment.Change,
            NewQuantity = adjustment.NewQuantity,
            Reason = adjustment.Reason,
            StaffId = staffId,
            AdjustedAt = at,
        };
    }

    public static StockAdjustment ToDto(StockAdjustmentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new StockAdjustment(record.Branch, record.Sku, record.Change, record.NewQuantity, record.Reason);
    }
}
