using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>32 products: packaging (PKG-), stationery (STA-), office supplies (OFF-), in every tax category.</summary>
public static class DemoProducts
{
    private static readonly (string Sku, string Name, decimal Price, TaxCategory Tax)[] Packaging =
    [
        ("PKG-0001", "Padded envelope A5", 1.20m, TaxCategory.Standard),
        ("PKG-0002", "Padded envelope A4", 1.80m, TaxCategory.Standard),
        ("PKG-0003", "Small parcel box", 2.50m, TaxCategory.Standard),
        ("PKG-0004", "Medium parcel box", 3.40m, TaxCategory.Standard),
        ("PKG-0005", "Large parcel box", 4.60m, TaxCategory.Standard),
        ("PKG-0006", "Document tube", 3.10m, TaxCategory.Standard),
        ("PKG-0007", "Bubble wrap roll 3 m", 4.20m, TaxCategory.Standard),
        ("PKG-0008", "Packing tape", 2.30m, TaxCategory.Standard),
        ("PKG-0009", "Fragile labels (10)", 1.50m, TaxCategory.Standard),
        ("PKG-0010", "Recycled mailing bag", 0.90m, TaxCategory.Reduced),
        ("PKG-0011", "Wine bottle box", 5.20m, TaxCategory.Standard),
        ("PKG-0012", "Cardboard divider set", 2.10m, TaxCategory.Reduced),
    ];

    private static readonly (string Sku, string Name, decimal Price, TaxCategory Tax)[] Stationery =
    [
        ("STA-0001", "Greeting card", 2.95m, TaxCategory.Standard),
        ("STA-0002", "Writing pad A4", 3.60m, TaxCategory.Reduced),
        ("STA-0003", "Envelopes C5 (25)", 3.20m, TaxCategory.Standard),
        ("STA-0004", "Ballpoint pens (4)", 2.40m, TaxCategory.Standard),
        ("STA-0005", "Notebook A5", 4.10m, TaxCategory.Reduced),
        ("STA-0006", "Postcard set (6)", 4.50m, TaxCategory.Standard),
        ("STA-0007", "Address book", 6.80m, TaxCategory.Reduced),
        ("STA-0008", "Gift wrap roll", 3.30m, TaxCategory.Standard),
        ("STA-0009", "Children's exercise book", 1.40m, TaxCategory.Exempt),
        ("STA-0010", "Calendar", 7.90m, TaxCategory.Standard),
    ];

    private static readonly (string Sku, string Name, decimal Price, TaxCategory Tax)[] OfficeSupplies =
    [
        ("OFF-0001", "Printer paper 500 sheets", 5.90m, TaxCategory.Standard),
        ("OFF-0002", "Stapler", 6.40m, TaxCategory.Standard),
        ("OFF-0003", "Staples (1000)", 1.70m, TaxCategory.Standard),
        ("OFF-0004", "Ring binder", 2.80m, TaxCategory.Standard),
        ("OFF-0005", "Sticky notes", 2.20m, TaxCategory.Standard),
        ("OFF-0006", "Highlighters (4)", 3.50m, TaxCategory.Standard),
        ("OFF-0007", "Calculator", 9.90m, TaxCategory.Standard),
        ("OFF-0008", "USB stick 32 GB", 8.50m, TaxCategory.Standard),
        ("OFF-0009", "Braille writing paper", 4.00m, TaxCategory.Exempt),
        ("OFF-0010", "Prepaid return envelope", 3.75m, TaxCategory.Exempt),
    ];

    public static IReadOnlyList<Product> Create() =>
    [
        .. Rows(Packaging, ProductCategory.Packaging),
        .. Rows(Stationery, ProductCategory.Stationery),
        .. Rows(OfficeSupplies, ProductCategory.OfficeSupplies),
    ];

    private static IEnumerable<Product> Rows(
        (string Sku, string Name, decimal Price, TaxCategory Tax)[] rows,
        ProductCategory category) =>
        rows.Select(row => new Product(new Sku(row.Sku), row.Name, category, new Money(row.Price), row.Tax));
}
