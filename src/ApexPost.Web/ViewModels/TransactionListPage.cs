using ApexPost.Application.DTOs;
using ApexPost.Domain.Transactions;

namespace ApexPost.Web.ViewModels;

public sealed record TransactionListPage(
    PagedResult<TransactionSummary> Result,
    TransactionStatus? Status,
    BranchFilter Filter,
    bool CanTransact);
