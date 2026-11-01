using ApexPost.Application.Validation;
using ApexPost.Domain.Common;

namespace ApexPost.Application.DTOs;

public sealed record LoginCommand
{
    [NotBlank]
    [TextLength(TextLimits.Email)]
    public string Email { get; init; } = string.Empty;

    [NotBlank]
    [TextLength(PasswordCommand.MaximumLength)]
    public string Password { get; init; } = string.Empty;

    public override string ToString() => $"{nameof(LoginCommand)} {{ {nameof(Email)} = {Email} }}";
}
