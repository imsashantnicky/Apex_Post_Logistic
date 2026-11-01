using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

public sealed class StockItemMapper : IRecordMapper<StockItem, StockKey, StockItemRecord>
{
    public StockKey IdOf(StockItemRecord record) => new(new BranchCode(record.Branch), new Sku(record.Sku));

    public StockItemRecord ToRecord(StockItem entity, int version) => new()
    {
        Branch = entity.Branch.Value,
        Sku = entity.Sku.Value,
        Quantity = entity.Quantity,
        Version = version,
    };

    public StockItem ToEntity(StockItemRecord record) =>
        new(new BranchCode(record.Branch), new Sku(record.Sku), record.Quantity) { Version = record.Version };
}
