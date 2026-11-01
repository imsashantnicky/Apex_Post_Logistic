using ApexPost.Domain.Personnel;

namespace ApexPost.Infrastructure.Persistence.Records;

/// <summary>A row of the Roles reference table; one row per <see cref="StaffRole"/> value.</summary>
public sealed class RoleRecord
{
    public StaffRole Name { get; set; }
}
