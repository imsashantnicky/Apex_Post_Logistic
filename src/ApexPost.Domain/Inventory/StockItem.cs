using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Inventory;

/// <summary>The quantity of one product held by one branch. It never goes below zero.</summary>
public sealed class StockItem : Entity<StockKey>
{
    public StockItem(BranchCode branch, Sku sku, int quantity)
        : base(new StockKey(branch, sku))
    {
        Quantity = quantity >= 0 ? quantity : throw new DomainException("A stock quantity cannot be negative.");
    }

    public BranchCode Branch => Id.Branch;

    public Sku Sku => Id.Sku;

    public int Quantity { get; private set; }

    public bool CanSupply(int quantity) => quantity <= Quantity;

    public void Add(int quantity) => Quantity = checked(Quantity + Guard.Positive(quantity, "Quantity"));

    public void Remove(int quantity)
    {
        Guard.Positive(quantity, "Quantity");
        if (!CanSupply(quantity))
        {
            throw new InsufficientStockException(Sku, quantity, Quantity);
        }

        Quantity -= quantity;
    }
}
