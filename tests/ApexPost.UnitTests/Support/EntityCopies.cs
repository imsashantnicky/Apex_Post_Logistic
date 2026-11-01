using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;

namespace ApexPost.UnitTests.Support;

/// <summary>
/// Copies entities with a new version, as a real store does when it reads a record. The fake repository keeps
/// copies, so a change that is not saved never leaks into the "stored" state.
/// </summary>
internal static class EntityCopies
{
    public static Branch Copy(Branch branch, int version) =>
        new(branch.Code, branch.Name, branch.Region, branch.OpensAt, branch.ClosesAt, branch.Counters, branch.IsActive)
        {
            Version = version,
        };

    public static Staff Copy(Staff staff, int version) =>
        new(staff.Id, staff.Name, staff.Email, staff.Role, staff.HomeBranch, staff.IsActive) { Version = version };

    public static Product Copy(Product product, int version) =>
        new(product.Sku, product.Name, product.Category, product.UnitPrice, product.TaxCategory)
        {
            Version = version,
        };

    public static StockItem Copy(StockItem item, int version) =>
        new(item.Branch, item.Sku, item.Quantity) { Version = version };

    public static ShippingZone Copy(ShippingZone zone, int version) =>
        new(zone.Code, zone.Name, zone.BaseRate, zone.PerKgRate) { Version = version };

    public static CounterTransaction Copy(CounterTransaction transaction, int version) =>
        CounterTransaction.Restore(
            transaction.Id,
            transaction.Branch,
            transaction.CounterId,
            transaction.ClerkId,
            transaction.OpenedAt,
            transaction.Status,
            transaction.Lines,
            transaction.Verifications,
            transaction.Payment,
            transaction.Reversal,
            version);
}
