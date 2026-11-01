using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Abstractions;

/// <summary>The signed-in staff member, taken from the StaffId, Role and BranchCode claims.</summary>
public interface ICurrentUser
{
    Guid StaffId { get; }

    StaffRole Role { get; }

    BranchCode Branch { get; }
}
