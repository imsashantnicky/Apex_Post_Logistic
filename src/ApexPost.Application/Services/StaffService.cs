using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Application.Settings;
using ApexPost.Application.Validation;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;
using Microsoft.Extensions.Options;

namespace ApexPost.Application.Services;

/// <summary>Staff administration for administrators: add, edit, (de)activate and set passwords.</summary>
public sealed class StaffService(
    ICurrentUser user,
    IStaffRepository staff,
    IStaffQueries queries,
    IBranchRepository branches,
    ICredentialStore credentials,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    AttributeModelValidator validator,
    IOptions<PasswordSettings> passwordSettings)
{
    public int MinimumPasswordLength => passwordSettings.Value.MinimumLength;

    public Task<IReadOnlyList<StaffSummary>> ListAsync(CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        return queries.ListAsync(cancellationToken);
    }

    public async Task<StaffSummary> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        return await queries.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Staff member {id} does not exist.");
    }

    public async Task<Guid> CreateAsync(
        StaffCommand details,
        PasswordCommand password,
        CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        validator.EnsureValid(details);
        EnsureStrong(password);
        var homeBranch = await ExistingBranchAsync(details.HomeBranch, cancellationToken).ConfigureAwait(false);
        await EnsureEmailFreeAsync(details.Email, null, cancellationToken).ConfigureAwait(false);

        var member = new Staff(Guid.CreateVersion7(), details.Name, details.Email, details.Role, homeBranch);
        staff.Add(member);
        await credentials.SetHashAsync(member.Id, hasher.Hash(password.Password), cancellationToken)
            .ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return member.Id;
    }

    public async Task UpdateAsync(
        Guid id,
        int expectedVersion,
        StaffCommand details,
        CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        validator.EnsureValid(details);
        var member = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        var homeBranch = await ExistingBranchAsync(details.HomeBranch, cancellationToken).ConfigureAwait(false);
        await EnsureEmailFreeAsync(details.Email, id, cancellationToken).ConfigureAwait(false);
        EnsureNotOwnRole(member, details.Role);

        member.Name = details.Name;
        member.Email = details.Email;
        member.Role = details.Role;
        member.HomeBranch = homeBranch;
        staff.Update(member, expectedVersion);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DeactivateAsync(
        Guid id,
        int expectedVersion,
        DeactivationCommand command,
        CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        if (id == user.StaffId)
        {
            throw new ForbiddenException("You cannot deactivate your own account.");
        }

        validator.EnsureValid(command);
        return ChangeAsync(id, expectedVersion, member => member.Deactivate(), cancellationToken);
    }

    public Task ActivateAsync(Guid id, int expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(id, expectedVersion, member => member.Activate(), cancellationToken);

    public async Task SetPasswordAsync(Guid id, PasswordCommand password, CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        EnsureStrong(password);
        var member = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        await credentials.SetHashAsync(member.Id, hasher.Hash(password.Password), cancellationToken)
            .ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ChangeAsync(
        Guid id,
        int expectedVersion,
        Action<Staff> change,
        CancellationToken cancellationToken)
    {
        user.RequireRole(StaffRole.Administrator);
        var member = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        change(member);
        staff.Update(member, expectedVersion);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>With this rule and no self-deactivation, the acting administrator always stays one.</summary>
    private void EnsureNotOwnRole(Staff member, StaffRole role)
    {
        if (member.Id == user.StaffId && member.Role != role)
        {
            throw new ValidationFailedException(nameof(StaffCommand.Role), "You cannot change your own role.");
        }
    }

    private void EnsureStrong(PasswordCommand password)
    {
        validator.EnsureValid(password);
        if (password.Password.Length < MinimumPasswordLength)
        {
            throw new ValidationFailedException(
                nameof(password.Password),
                $"Password must be at least {MinimumPasswordLength} characters.");
        }
    }

    private async Task<BranchCode> ExistingBranchAsync(string code, CancellationToken cancellationToken)
    {
        var branch = new BranchCode(code);
        return await branches.GetAsync(branch, cancellationToken).ConfigureAwait(false) is not null
            ? branch
            : throw new ValidationFailedException(nameof(StaffCommand.HomeBranch), $"Branch {branch} does not exist.");
    }

    private async Task EnsureEmailFreeAsync(string email, Guid? ownId, CancellationToken cancellationToken)
    {
        if (await staff.EmailTakenAsync(email.Trim(), ownId, cancellationToken).ConfigureAwait(false))
        {
            throw new ValidationFailedException(nameof(StaffCommand.Email), "This email is already in use.");
        }
    }

    private async Task<Staff> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await staff.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Staff member {id} does not exist.");
}
