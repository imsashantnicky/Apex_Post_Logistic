using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;

namespace ApexPost.Domain.Events;

public sealed record StockLow(BranchCode Branch, Sku Sku, int Quantity, DateTimeOffset OccurredAt) : IDomainEvent;
