using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;

namespace ApexPost.Domain.Transactions;

/// <summary>The configurable rules applied when a basket is paid.</summary>
public sealed record PaymentRules(
    Money VerificationThreshold,
    Func<ILineItem, Money> TaxRule,
    Predicate<StockItem> IsLowStock)
{
    public Money VerificationThreshold { get; } = Guard.Required(VerificationThreshold, "Verification threshold");

    public Func<ILineItem, Money> TaxRule { get; } = Guard.Required(TaxRule, "Tax rule");

    public Predicate<StockItem> IsLowStock { get; } = Guard.Required(IsLowStock, "Low-stock rule");
}
