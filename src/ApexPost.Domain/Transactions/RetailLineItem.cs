using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

/// <summary>A quantity of a catalogue product; name, price and tax category are copied at the time of sale.</summary>
public sealed record RetailLineItem : TransactionLine
{
    public RetailLineItem(Guid id, Sku sku, string name, Money unitPrice, int quantity, TaxCategory taxCategory)
        : base(id)
    {
        Sku = Guard.Required(sku, nameof(Sku));
        Name = Guard.NotBlank(name, nameof(Name), TextLimits.Name);
        UnitPrice = Guard.Required(unitPrice, "Unit price");
        Quantity = Guard.Positive(quantity, nameof(Quantity));
        TaxCategory = Guard.Defined(taxCategory, nameof(TaxCategory));
    }

    public Sku Sku { get; }

    public string Name { get; }

    public Money UnitPrice { get; }

    public int Quantity { get; }

    public override TaxCategory TaxCategory { get; }

    public override string Description => Name;

    public override Money Total => UnitPrice * Quantity;

    public static RetailLineItem For(Product product, int quantity) =>
        new(Guid.CreateVersion7(), product.Sku, product.Name, product.UnitPrice, quantity, product.TaxCategory);
}
