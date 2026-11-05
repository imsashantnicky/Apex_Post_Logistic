using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.Api.Composition;

/// <summary>
/// The current user of the API host. The API has no staff sign-in, so a Week 3 write that asks who the staff member
/// is fails with <see cref="ForbiddenException"/> (403) instead of being stamped with a made-up user.
/// </summary>
public sealed class NoSignedInStaff : ICurrentUser
{
    private const string Message = "The API has no signed-in staff member.";

    /// <summary>Always throws <see cref="ForbiddenException"/>: there is no signed-in staff member.</summary>
    public Guid StaffId => throw new ForbiddenException(Message);

    /// <summary>Always throws <see cref="ForbiddenException"/>: there is no signed-in staff member.</summary>
    public StaffRole Role => throw new ForbiddenException(Message);

    /// <summary>Always throws <see cref="ForbiddenException"/>: there is no signed-in staff member.</summary>
    public BranchCode Branch => throw new ForbiddenException(Message);
}
