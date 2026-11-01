using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>
/// Every product in every branch. Every ninth product (from the fifth on) is kept at 2–5 items, so the
/// low-stock report and dashboard have rows; the others hold 30–70.
/// </summary>
public static class DemoStock
{
    private const int LowEvery = 9;
    private const int LowOffset = 4;
    private const int LowBase = 2;
    private const int LowSpread = 4;
    private const int NormalBase = 30;
    private const int NormalSpread = 41;
    private const int BranchStep = 7;
    private const int ProductStep = 13;

    public static bool IsKeptLow(int productIndex) => productIndex % LowEvery == LowOffset;

    public static List<StockItem> Create(IReadOnlyList<Branch> branches, IReadOnlyList<Product> products) =>
    [
        .. branches.SelectMany((branch, branchIndex) => products.Select((product, productIndex) =>
            new StockItem(branch.Code, product.Sku, Quantity(branchIndex, productIndex)))),
    ];

    private static int Quantity(int branchIndex, int productIndex) =>
        IsKeptLow(productIndex)
            ? LowBase + (branchIndex % LowSpread)
            : NormalBase + (((branchIndex * BranchStep) + (productIndex * ProductStep)) % NormalSpread);
}
