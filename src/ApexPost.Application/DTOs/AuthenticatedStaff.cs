using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.DTOs;

/// <summary>The signed-in staff member; the web layer turns it into the StaffId, Role and BranchCode claims.</summary>
public sealed record AuthenticatedStaff(Guid StaffId, string Name, StaffRole Role, BranchCode Branch);
