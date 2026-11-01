using ApexPost.Domain.Shipping;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

public sealed class ZoneMapper : IRecordMapper<ShippingZone, string, ZoneRecord>
{
    public string IdOf(ZoneRecord record) => record.Code.ToUpperInvariant();

    public ZoneRecord ToRecord(ShippingZone entity, int version) => new()
    {
        Code = entity.Code,
        Name = entity.Name,
        BaseRate = entity.BaseRate.ToRecord(),
        PerKgRate = entity.PerKgRate.ToRecord(),
        Version = version,
    };

    public ShippingZone ToEntity(ZoneRecord record) =>
        new(record.Code, record.Name, record.BaseRate.ToMoney(), record.PerKgRate.ToMoney())
        {
            Version = record.Version,
        };
}
