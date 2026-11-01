using ApexPost.Domain.Common;

namespace ApexPost.Domain.Catalogue;

/// <summary>A retail product sold at the counter.</summary>
public sealed class Product : Entity<Sku>
{
    public Product(Sku sku, string name, ProductCategory category, Money unitPrice, TaxCategory taxCategory)
        : base(sku)
    {
        Name = Guard.NotBlank(name, nameof(Name), TextLimits.Name);
        Category = Guard.Defined(category, nameof(Category));
        UnitPrice = Guard.Required(unitPrice, "Unit price").IsPositive
            ? unitPrice
            : throw new DomainException("A unit price must be greater than zero.");
        TaxCategory = Guard.Defined(taxCategory, nameof(TaxCategory));
    }

    // Only for persistence frameworks; they fill the properties from stored values.
    private Product()
    {
        Name = null!;
        UnitPrice = null!;
    }

    public Sku Sku => Id;

    public string Name { get; }

    public ProductCategory Category { get; }

    public Money UnitPrice { get; }

    public TaxCategory TaxCategory { get; }
}
