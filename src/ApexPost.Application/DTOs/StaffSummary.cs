using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.DTOs;

public sealed record StaffSummary(
    Guid Id,
    string Name,
    string Email,
    StaffRole Role,
    BranchCode HomeBranch,
    bool IsActive,
    int Version)
{
    public static StaffSummary From(Staff staff) =>
        new(staff.Id, staff.Name, staff.Email, staff.Role, staff.HomeBranch, staff.IsActive, staff.Version);
}
