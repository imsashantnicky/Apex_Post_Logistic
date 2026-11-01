using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Events;

public sealed record MoneyOrderIssued(
    Guid TransactionId,
    BranchCode Branch,
    Guid LineId,
    Money Amount,
    DateTimeOffset OccurredAt) : IDomainEvent;
