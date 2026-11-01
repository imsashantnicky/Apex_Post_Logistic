using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

/// <summary>Who voided or refunded a transaction, why and when.</summary>
public sealed record Reversal(Guid ManagerId, string Reason, DateTimeOffset ReversedAt)
{
    public Guid ManagerId { get; } = Guard.NotEmpty(ManagerId, "Manager id");

    public string Reason { get; } = Guard.NotBlank(Reason, nameof(Reason), TextLimits.Reason);
}
