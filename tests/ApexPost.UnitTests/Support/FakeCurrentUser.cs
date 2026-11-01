using ApexPost.Application.Abstractions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.UnitTests.Support;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid StaffId { get; set; }

    public StaffRole Role { get; set; }

    public BranchCode Branch { get; set; } = new(TestData.HomeBranch);

    public void SignInAs(Staff staff)
    {
        StaffId = staff.Id;
        Role = staff.Role;
        Branch = staff.HomeBranch;
    }
}
