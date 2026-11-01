using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;

namespace ApexPost.Application.Reports;

/// <summary>Paid sales of one branch on one UTC day, split into retail goods and services.</summary>
public sealed record DailySalesRow(
    DateOnly Day,
    BranchCode Branch,
    int Transactions,
    Money Retail,
    Money Services,
    Money Tax,
    Money Total);
