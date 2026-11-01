using ApexPost.Application.Validation;

namespace ApexPost.Application.DTOs;

/// <summary>A new password; the minimum length comes from the Passwords settings.</summary>
public sealed record PasswordCommand
{
    public const int MaximumLength = 128;

    [NotBlank]
    [TextLength(MaximumLength)]
    public string Password { get; init; } = string.Empty;

    public override string ToString() => nameof(PasswordCommand);
}
