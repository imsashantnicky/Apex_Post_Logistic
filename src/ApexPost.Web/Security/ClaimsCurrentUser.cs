using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.Web.Security;

/// <summary>The signed-in staff member of the current request, read from the claims.</summary>
public sealed class ClaimsCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private const string NotSignedIn = "No staff member is signed in.";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid StaffId => Principal?.StaffId ?? throw new ForbiddenException(NotSignedIn);

    public StaffRole Role => Principal?.Role ?? throw new ForbiddenException(NotSignedIn);

    public BranchCode Branch => Principal?.BranchCode ?? throw new ForbiddenException(NotSignedIn);
}
