using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Events;

public sealed record TransactionRefunded(
    Guid TransactionId,
    BranchCode Branch,
    Guid ManagerId,
    string Reason,
    Money Total,
    DateTimeOffset OccurredAt) : IDomainEvent;
