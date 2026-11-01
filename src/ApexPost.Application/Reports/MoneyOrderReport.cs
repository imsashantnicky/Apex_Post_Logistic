using ApexPost.Application.DTOs;

namespace ApexPost.Application.Reports;

/// <summary>The auditor view: totals per branch and one page of the transactions behind them.</summary>
public sealed record MoneyOrderReport(
    IReadOnlyList<MoneyOrderBranchRow> Branches,
    PagedResult<MoneyOrderRow> Transactions);
