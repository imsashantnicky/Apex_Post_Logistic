using ApexPost.Domain.Branches;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

/// <summary>Stores a transaction and rebuilds it through <see cref="CounterTransaction.Restore"/>.</summary>
public sealed class TransactionMapper : IRecordMapper<CounterTransaction, Guid, TransactionRecord>
{
    public Guid IdOf(TransactionRecord record) => record.Id;

    public TransactionRecord ToRecord(CounterTransaction entity, int version) => new()
    {
        Id = entity.Id,
        Branch = entity.Branch.Value,
        CounterId = entity.CounterId,
        ClerkId = entity.ClerkId,
        OpenedAt = entity.OpenedAt,
        Status = entity.Status,
        Lines = [.. entity.Lines.Select(LineItemMapping.ToRecord)],
        Verifications = [.. entity.Verifications.Select(ToRecord)],
        Payment = entity.Payment is { } payment
            ? new PaymentRecord { Method = payment.Method, Tax = payment.Tax.ToRecord(), PaidAt = payment.PaidAt }
            : null,
        Reversal = entity.Reversal is { } reversal
            ? new ReversalRecord
            {
                ManagerId = reversal.ManagerId,
                Reason = reversal.Reason,
                ReversedAt = reversal.ReversedAt,
            }
            : null,
        Version = version,
    };

    public CounterTransaction ToEntity(TransactionRecord record) =>
        CounterTransaction.Restore(
            record.Id,
            new BranchCode(record.Branch),
            record.CounterId,
            record.ClerkId,
            record.OpenedAt,
            record.Status,
            record.Lines.Select(LineItemMapping.ToLine),
            record.Verifications.Select(verification => new IdentityVerification(
                verification.DocumentType,
                verification.MaskedNumber,
                verification.Result,
                verification.VerifiedAt)),
            record.Payment is { } payment ? new Payment(payment.Method, payment.Tax.ToMoney(), payment.PaidAt) : null,
            record.Reversal is { } reversal
                ? new Reversal(reversal.ManagerId, reversal.Reason, reversal.ReversedAt)
                : null,
            record.Version);

    private static VerificationRecord ToRecord(IdentityVerification verification) => new()
    {
        DocumentType = verification.DocumentType,
        MaskedNumber = verification.MaskedNumber,
        Result = verification.Result,
        VerifiedAt = verification.VerifiedAt,
    };
}
