using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Services;

/// <summary>
/// Checks email and password. Unknown emails are verified against a dummy hash, so a wrong email takes as
/// long as a wrong password and the response does not reveal which accounts exist. Inactive staff cannot
/// sign in.
/// </summary>
public sealed class LoginService(
    IStaffRepository staff,
    ICredentialStore credentials,
    IPasswordHasher hasher,
    DummyPasswordHash dummyHash)
{
    /// <summary>The signed-in staff member, or null for any failure (one generic error for the user).</summary>
    public async Task<AuthenticatedStaff?> SignInAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var member = await staff.FindByEmailAsync(command.Email.Trim(), cancellationToken).ConfigureAwait(false);
        var hash = member is null
            ? null
            : await credentials.GetHashAsync(member.Id, cancellationToken).ConfigureAwait(false);

        var passwordMatches = hasher.Verify(command.Password, hash ?? dummyHash.Value);
        return passwordMatches && hash is not null && member is { CanSignIn: true } ? ToAuthenticated(member) : null;
    }

    /// <summary>Reloads the staff member on each request, so a deactivated account loses access at once.</summary>
    public async Task<AuthenticatedStaff?> RevalidateAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var member = await staff.GetAsync(staffId, cancellationToken).ConfigureAwait(false);
        return member is { CanSignIn: true } ? ToAuthenticated(member) : null;
    }

    private static AuthenticatedStaff ToAuthenticated(Staff member) =>
        new(member.Id, member.Name, member.Role, member.HomeBranch);
}
