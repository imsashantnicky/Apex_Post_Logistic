using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

/// <summary>The common base of the basket lines, so that a transaction's lines can be stored as one set.</summary>
public abstract record TransactionLine : ILineItem
{
    protected TransactionLine(Guid id)
    {
        Id = Guard.NotEmpty(id, "Line id");
    }

    public Guid Id { get; }

    public abstract string Description { get; }

    public abstract Money Total { get; }

    public abstract TaxCategory TaxCategory { get; }
}
