using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;

namespace ApexPost.Application.Reports;

/// <summary>One branch's money orders issued in the period and its identity checks by result.</summary>
public sealed record MoneyOrderBranchRow(
    BranchCode Branch,
    int MoneyOrders,
    Money Amount,
    Money Fees,
    int ChecksPassed,
    int ChecksFailed,
    int ChecksPending);
