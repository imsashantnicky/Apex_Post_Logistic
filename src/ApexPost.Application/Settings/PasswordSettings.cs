namespace ApexPost.Application.Settings;

/// <summary>The "Passwords" section: PBKDF2 work factor and the minimum password length.</summary>
public sealed class PasswordSettings
{
    public const string SectionName = "Passwords";

    public int Iterations { get; set; } = 600_000;

    public int MinimumLength { get; set; } = 12;
}
