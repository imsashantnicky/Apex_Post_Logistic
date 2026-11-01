using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Persistence.Records;

namespace ApexPost.Infrastructure.Persistence.Mappers;

/// <summary>Maps an identity check to its row and back; the row keeps only the last 4 characters.</summary>
public static class IdentityVerificationMapper
{
    public static IdentityVerificationRecord ToRecord(Guid transactionId, IdentityVerification verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        return new IdentityVerificationRecord
        {
            Id = Guid.CreateVersion7(),
            TransactionId = transactionId,
            DocumentType = verification.DocumentType,
            DocumentLast4 = verification.MaskedNumber[IdentityVerification.MaskPrefix.Length..],
            Result = verification.Result,
            VerifiedAt = verification.VerifiedAt,
        };
    }

    public static IdentityVerification ToDomain(IdentityVerificationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new IdentityVerification(
            record.DocumentType,
            IdentityVerification.MaskPrefix + record.DocumentLast4,
            record.Result,
            record.VerifiedAt);
    }
}
