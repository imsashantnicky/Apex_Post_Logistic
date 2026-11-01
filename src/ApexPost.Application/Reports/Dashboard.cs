using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;

namespace ApexPost.Application.Reports;

/// <summary>Today's figures, low stock and the latest alerts of a branch (null = all branches).</summary>
public sealed record Dashboard(
    BranchCode? Branch,
    DateOnly Day,
    int Transactions,
    Money Retail,
    Money Services,
    Money Total,
    LowStockReport LowStock,
    IReadOnlyList<Notification> Alerts);
