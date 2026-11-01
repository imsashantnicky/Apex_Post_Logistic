using ApexPost.Application.Settings;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace ApexPost.Application.Services;

/// <summary>The configurable payment rules: verification threshold, tax rule and low-stock predicate.</summary>
public sealed class CheckoutRules
{
    public CheckoutRules(
        TaxCalculator tax,
        IOptions<MoneyOrderSettings> moneyOrders,
        IOptions<StockSettings> stock)
    {
        ArgumentNullException.ThrowIfNull(tax);
        VerificationThreshold = new Money(moneyOrders.Value.IdentityVerificationThreshold);
        IsLowStock = LowStockRule.AtOrBelow(stock.Value.LowStockThreshold);
        TaxRule = tax.Rule;
        Payment = new PaymentRules(VerificationThreshold, TaxRule, IsLowStock);
    }

    public Money VerificationThreshold { get; }

    public Predicate<StockItem> IsLowStock { get; }

    public Func<ILineItem, Money> TaxRule { get; }

    public PaymentRules Payment { get; }
}
