using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;

namespace ApexPost.Domain.Events;

public sealed record TransactionCompleted(
    Guid TransactionId,
    BranchCode Branch,
    Money Total,
    PaymentMethod Method,
    DateTimeOffset OccurredAt) : IDomainEvent;
