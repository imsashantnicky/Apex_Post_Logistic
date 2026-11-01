using ApexPost.Application.Validation;
using ApexPost.Domain.Verification;

namespace ApexPost.Application.DTOs;

/// <summary>
/// The document shown by the customer. The full number only travels to the verification service;
/// it is masked before anything is stored, and it must never be logged or audited.
/// </summary>
public sealed record VerifyIdentityCommand
{
    public DocumentType DocumentType { get; init; } = DocumentType.Passport;

    [NotBlank]
    [Pattern(InputPatterns.DocumentNumber, InputPatterns.DocumentNumberExample)]
    public string DocumentNumber { get; init; } = string.Empty;

    public override string ToString() => $"{nameof(VerifyIdentityCommand)} {{ {DocumentType} }}";
}
