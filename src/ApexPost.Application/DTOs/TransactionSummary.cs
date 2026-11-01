using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.DTOs;

/// <summary>One row of the transaction list.</summary>
public sealed record TransactionSummary(
    Guid Id,
    BranchCode Branch,
    DateTimeOffset OpenedAt,
    TransactionStatus Status,
    int LineCount,
    Money Total);
