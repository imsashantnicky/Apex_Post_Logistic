using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

/// <summary>A quantity of a catalogue product; name, price and tax category are copied at the time of sale.</summary>
public sealed record RetailLineItem(
    Guid Id,
    Sku Sku,
    string Name,
    Money UnitPrice,
    int Quantity,
    TaxCategory TaxCategory) : ILineItem
{
    public Guid Id { get; } = Guard.NotEmpty(Id, "Line id");

    public Sku Sku { get; } = Guard.Required(Sku, nameof(Sku));

    public string Name { get; } = Guard.NotBlank(Name, nameof(Name), TextLimits.Name);

    public Money UnitPrice { get; } = Guard.Required(UnitPrice, "Unit price");

    public int Quantity { get; } = Guard.Positive(Quantity, nameof(Quantity));

    public TaxCategory TaxCategory { get; } = Guard.Defined(TaxCategory, nameof(TaxCategory));

    public string Description => Name;

    public Money Total => UnitPrice * Quantity;

    public static RetailLineItem For(Product product, int quantity) =>
        new(Guid.CreateVersion7(), product.Sku, product.Name, product.UnitPrice, quantity, product.TaxCategory);
}
