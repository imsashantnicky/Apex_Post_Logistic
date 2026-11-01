using ApexPost.Domain.Branches;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

public sealed class BranchMapper : IRecordMapper<Branch, BranchCode, BranchRecord>
{
    public BranchCode IdOf(BranchRecord record) => new(record.Code);

    public BranchRecord ToRecord(Branch entity, int version) => new()
    {
        Code = entity.Code.Value,
        Name = entity.Name,
        Region = entity.Region,
        OpensAt = entity.OpensAt,
        ClosesAt = entity.ClosesAt,
        IsActive = entity.IsActive,
        Counters =
        [
            .. entity.Counters.Select(counter => new CounterRecord { Id = counter.Id, Number = counter.Number }),
        ],
        Version = version,
    };

    public Branch ToEntity(BranchRecord record) =>
        new(
            IdOf(record),
            record.Name,
            record.Region,
            record.OpensAt,
            record.ClosesAt,
            record.Counters.Select(counter => new Counter(counter.Id, counter.Number)),
            record.IsActive)
        {
            Version = record.Version,
        };
}
