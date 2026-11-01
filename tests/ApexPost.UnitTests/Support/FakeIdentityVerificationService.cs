using ApexPost.Application.Abstractions;
using ApexPost.Domain.Verification;

namespace ApexPost.UnitTests.Support;

internal sealed class FakeIdentityVerificationService : IIdentityVerificationService
{
    public VerificationResult Result { get; set; } = VerificationResult.Passed;

    public Task<VerificationResult> VerifyAsync(
        DocumentType documentType,
        string documentNumber,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result);
}
