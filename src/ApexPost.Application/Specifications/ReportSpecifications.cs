using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Specifications;

/// <summary>Turns report and list filters into specifications.</summary>
public static class ReportSpecifications
{
    public static PredicateSpecification<CounterTransaction> InBranch(BranchCode? branch) =>
        new(transaction => branch is null || transaction.Branch == branch);

    public static PredicateSpecification<CounterTransaction> Transactions(ReportFilter filter) =>
        InBranch(filter.Branch).And(new PredicateSpecification<CounterTransaction>(
            transaction => filter.Covers(transaction.Payment?.PaidAt ?? transaction.OpenedAt)));

    public static PredicateSpecification<CounterTransaction> PaidTransactions(ReportFilter filter) =>
        Transactions(filter).And(WithStatus(TransactionStatus.Paid));

    public static PredicateSpecification<CounterTransaction> WithStatus(TransactionStatus status) =>
        new(transaction => transaction.Status == status);

    public static PredicateSpecification<CounterTransaction> Matching(TransactionFilter filter) =>
        InBranch(filter.Branch).And(new PredicateSpecification<CounterTransaction>(transaction =>
        {
            var day = DateOnly.FromDateTime(transaction.OpenedAt.UtcDateTime);
            return (filter.From is null || day >= filter.From)
                && (filter.To is null || day <= filter.To)
                && (filter.Status is null || transaction.Status == filter.Status);
        }));

    public static PredicateSpecification<StockItem> LowStock(BranchCode? branch, Predicate<StockItem> isLowStock) =>
        new(item => (branch is null || item.Branch == branch) && isLowStock(item));
}
