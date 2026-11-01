using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Inventory;

/// <summary>Identifies the stock of one product in one branch, for example BR001/PKG-0001.</summary>
public sealed record StockKey(BranchCode Branch, Sku Sku)
{
    public BranchCode Branch { get; } = Guard.Required(Branch, nameof(Branch));

    public Sku Sku { get; } = Guard.Required(Sku, nameof(Sku));

    public override string ToString() => $"{Branch}/{Sku}";
}
