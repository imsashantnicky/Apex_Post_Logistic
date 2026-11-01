using ApexPost.Domain.Catalogue;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

public sealed class ProductMapper : IRecordMapper<Product, Sku, ProductRecord>
{
    public Sku IdOf(ProductRecord record) => new(record.Sku);

    public ProductRecord ToRecord(Product entity, int version) => new()
    {
        Sku = entity.Sku.Value,
        Name = entity.Name,
        Category = entity.Category,
        UnitPrice = entity.UnitPrice.ToRecord(),
        TaxCategory = entity.TaxCategory,
        Version = version,
    };

    public Product ToEntity(ProductRecord record) =>
        new(IdOf(record), record.Name, record.Category, record.UnitPrice.ToMoney(), record.TaxCategory)
        {
            Version = record.Version,
        };
}
