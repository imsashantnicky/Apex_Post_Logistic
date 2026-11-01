using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Services;

/// <summary>
/// Checks email and password. Unknown emails are verified against a dummy hash, so a wrong email takes as
/// long as a wrong password and the response does not reveal which accounts exist. Inactive staff cannot
/// sign in. Stateless apart from the dummy hash, so it can be a singleton.
/// </summary>
public sealed class LoginService(IRepository<Staff, Guid> staff, ICredentialStore credentials, IPasswordHasher hasher)
{
    private readonly Lazy<string> _dummyHash = new(() => hasher.Hash(Guid.NewGuid().ToString()));

    /// <summary>The signed-in staff member, or null for any failure (one generic error for the user).</summary>
    public async Task<AuthenticatedStaff?> SignInAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var email = command.Email.Trim();
        var byEmail = new PredicateSpecification<Staff>(member =>
            string.Equals(member.Email, email, StringComparison.OrdinalIgnoreCase));
        var matches = await staff.ListAsync(byEmail, cancellationToken).ConfigureAwait(false);
        var member = matches is [var first, ..] ? first : null;
        var hash = member is null
            ? null
            : await credentials.GetHashAsync(member.Id, cancellationToken).ConfigureAwait(false);

        var passwordMatches = hasher.Verify(command.Password, hash ?? _dummyHash.Value);
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
