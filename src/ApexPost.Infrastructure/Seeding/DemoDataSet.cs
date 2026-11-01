using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>
/// All demo data as plain domain objects, independent of storage: the JSON seeder saves it today and a
/// database seeder can save the same objects next week. Stock is the level after the historical sales.
/// </summary>
public sealed record DemoDataSet(
    IReadOnlyList<Branch> Branches,
    IReadOnlyList<Staff> Staff,
    IReadOnlyList<Product> Products,
    IReadOnlyList<ShippingZone> Zones,
    IReadOnlyList<StockItem> Stock,
    IReadOnlyList<CounterTransaction> Transactions)
{
    public static DemoDataSet Create(DateTimeOffset now, PaymentRules rules, ServiceLevelMultipliers multipliers)
    {
        var branches = DemoBranches.Create();
        var staff = DemoStaff.Create();
        var products = DemoProducts.Create();
        var zones = DemoZones.Create();
        var stock = DemoStock.Create(branches, products);
        var history = new DemoHistory(branches, staff, products, zones, stock, rules, multipliers).Create(now);
        return new DemoDataSet(branches, staff, products, zones, stock, history);
    }
}
