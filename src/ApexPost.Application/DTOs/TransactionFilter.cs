using ApexPost.Domain.Branches;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.DTOs;

/// <summary>Optional filters for the transaction list; null means "any".</summary>
public sealed record TransactionFilter(
    BranchCode? Branch = null,
    DateOnly? From = null,
    DateOnly? To = null,
    TransactionStatus? Status = null);
