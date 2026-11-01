using ApexPost.Domain.Transactions;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>
/// A counter transaction. Totals are not stored: they are recalculated from the lines and the frozen tax,
/// so a stored total can never disagree with its lines.
/// </summary>
public sealed record TransactionRecord : IVersionedRecord
{
    public required Guid Id { get; init; }

    public required string Branch { get; init; }

    public required Guid CounterId { get; init; }

    public required Guid ClerkId { get; init; }

    public required DateTimeOffset OpenedAt { get; init; }

    public required TransactionStatus Status { get; init; }

    public required IReadOnlyList<LineItemRecord> Lines { get; init; }

    public required IReadOnlyList<VerificationRecord> Verifications { get; init; }

    public PaymentRecord? Payment { get; init; }

    public ReversalRecord? Reversal { get; init; }

    public required int Version { get; init; }
}
