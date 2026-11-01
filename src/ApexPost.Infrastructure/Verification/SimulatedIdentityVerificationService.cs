using ApexPost.Application.Abstractions;
using ApexPost.Domain.Verification;

namespace ApexPost.Infrastructure.Verification;

/// <summary>
/// Stands in for an identity provider. The result depends on the document number only, never on the clerk:
/// numbers starting with FAIL fail, PEND stay pending, every other number passes. The number is not kept.
/// </summary>
public sealed class SimulatedIdentityVerificationService : IIdentityVerificationService
{
    public const string FailPrefix = "FAIL";
    public const string PendingPrefix = "PEND";

    public Task<VerificationResult> VerifyAsync(
        DocumentType documentType,
        string documentNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var number = documentNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        var result = number switch
        {
            _ when number.StartsWith(FailPrefix, StringComparison.Ordinal) => VerificationResult.Failed,
            _ when number.StartsWith(PendingPrefix, StringComparison.Ordinal) => VerificationResult.Pending,
            _ => VerificationResult.Passed,
        };
        return Task.FromResult(result);
    }
}
