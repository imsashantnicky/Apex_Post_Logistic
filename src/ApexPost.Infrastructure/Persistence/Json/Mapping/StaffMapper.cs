using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

public sealed class StaffMapper : IRecordMapper<Staff, Guid, StaffRecord>
{
    public Guid IdOf(StaffRecord record) => record.Id;

    public StaffRecord ToRecord(Staff entity, int version) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Email = entity.Email,
        Role = entity.Role,
        HomeBranch = entity.HomeBranch.Value,
        IsActive = entity.IsActive,
        Version = version,
    };

    public Staff ToEntity(StaffRecord record) =>
        new(record.Id, record.Name, record.Email, record.Role, new BranchCode(record.HomeBranch), record.IsActive)
        {
            Version = record.Version,
        };
}
