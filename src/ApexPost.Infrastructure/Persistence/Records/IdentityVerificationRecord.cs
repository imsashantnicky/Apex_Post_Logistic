using ApexPost.Domain.Verification;

namespace ApexPost.Infrastructure.Persistence.Records;

/// <summary>An identity check as stored: only the last 4 characters of the document number are kept.</summary>
public sealed class IdentityVerificationRecord
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public DocumentType DocumentType { get; set; }

    public string DocumentLast4 { get; set; } = string.Empty;

    public VerificationResult Result { get; set; }

    public DateTimeOffset VerifiedAt { get; set; }
}
