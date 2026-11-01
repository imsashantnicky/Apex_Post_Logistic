using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Reports;

/// <summary>
/// The auditor view: one transaction with money orders or identity checks. The verification shows only the
/// document type, the masked number and the result.
/// </summary>
public sealed record MoneyOrderRow(
    Guid TransactionId,
    BranchCode Branch,
    DateTimeOffset At,
    TransactionStatus Status,
    int MoneyOrders,
    Money Amount,
    Money Fees,
    string? Verification);
