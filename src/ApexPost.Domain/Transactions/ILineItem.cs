using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

/// <summary>One line of a mixed basket: a retail item, a shipment or a money order.</summary>
public interface ILineItem
{
    Guid Id { get; }

    string Description { get; }

    Money Total { get; }

    TaxCategory TaxCategory { get; }
}
