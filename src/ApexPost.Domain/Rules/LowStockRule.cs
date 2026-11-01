using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;

namespace ApexPost.Domain.Rules;

/// <summary>Builds the configurable low-stock predicate used after a sale and by the low-stock report.</summary>
public static class LowStockRule
{
    public static Predicate<StockItem> AtOrBelow(int threshold)
    {
        if (threshold < 0)
        {
            throw new DomainException("The low-stock threshold cannot be negative.");
        }

        return item => item.Quantity <= threshold;
    }
}
