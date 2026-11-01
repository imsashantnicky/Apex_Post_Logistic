using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Personnel;

/// <summary>A staff member. Name, email, role and home branch can be updated; each setter validates.</summary>
public sealed class Staff : Entity<Guid>
{
    private const char EmailSeparator = '@';
    private const char DomainSeparator = '.';

    public Staff(Guid id, string name, string email, StaffRole role, BranchCode homeBranch, bool isActive = true)
        : base(Guard.NotEmpty(id, "Staff id"))
    {
        Name = name;
        Email = email;
        Role = role;
        HomeBranch = homeBranch;
        IsActive = isActive;
    }

    public string Name { get; set => field = Guard.NotBlank(value, nameof(Name), TextLimits.Name); }

    public string Email { get; set => field = ValidEmail(value); }

    public StaffRole Role { get; set => field = Guard.Defined(value, nameof(Role)); }

    public BranchCode HomeBranch { get; set => field = Guard.Required(value, "Home branch"); }

    public bool IsActive { get; private set; }

    public bool CanSignIn => IsActive;

    /// <summary>Only active clerks and branch managers serve customers; auditors and administrators never do.</summary>
    public bool CanTransact => IsActive && Role is StaffRole.CounterClerk or StaffRole.BranchManager;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void EnsureCanTransactAt(BranchCode branch)
    {
        EnsureActive();
        if (!CanTransact)
        {
            throw new DomainException($"The {Role} role cannot process transactions.");
        }

        EnsureHomeBranch(branch);
    }

    /// <summary>Voids and refunds need an active Branch Manager of the same branch.</summary>
    public void EnsureCanReverseAt(BranchCode branch)
    {
        EnsureActive();
        if (Role != StaffRole.BranchManager)
        {
            throw new DomainException("Only a Branch Manager can void or refund a transaction.");
        }

        EnsureHomeBranch(branch);
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException("An inactive staff member cannot process transactions.");
        }
    }

    private void EnsureHomeBranch(BranchCode branch)
    {
        if (HomeBranch != branch)
        {
            throw new DomainException($"Staff of branch {HomeBranch} cannot work on transactions of {branch}.");
        }
    }

    private static string ValidEmail(string value)
    {
        var email = Guard.NotBlank(value, nameof(Email), TextLimits.Email);
        var isValid = email.Split(EmailSeparator) is [{ Length: > 0 }, var domain]
            && domain.Contains(DomainSeparator, StringComparison.Ordinal)
            && !domain.StartsWith(DomainSeparator)
            && !domain.EndsWith(DomainSeparator)
            && !email.Any(char.IsWhiteSpace);
        return isValid ? email : throw new DomainException("An email must look like name@example.com.");
    }
}
