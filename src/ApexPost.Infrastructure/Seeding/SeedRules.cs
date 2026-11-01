using ApexPost.Application.Settings;
using ApexPost.Domain.Common;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>The configured business rules that both seeders apply while they build the demo history.</summary>
public sealed class SeedRules(
    IOptions<TaxSettings> tax,
    IOptions<MoneyOrderSettings> moneyOrders,
    IOptions<StockSettings> stock,
    IOptions<ShippingSettings> shipping)
{
    public PaymentRules Payment =>
        new(
            new Money(moneyOrders.Value.IdentityVerificationThreshold),
            TaxRule.From(tax.Value.ToRates()),
            LowStockRule.AtOrBelow(stock.Value.LowStockThreshold));

    public ServiceLevelMultipliers Multipliers => shipping.Value.ToMultipliers();
}
