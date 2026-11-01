using ApexPost.Domain.Verification;

namespace ApexPost.Application.Abstractions;

/// <summary>Checks an identity document; the result comes from the service, never from the clerk.</summary>
public interface IIdentityVerificationService
{
    Task<VerificationResult> VerifyAsync(
        DocumentType documentType,
        string documentNumber,
        CancellationToken cancellationToken);
}
