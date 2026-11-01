using ApexPost.Application.Settings;
using ApexPost.Domain.Common;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace ApexPost.Application.Services;

/// <summary>
/// The tax rule built from the configured rates. The same rule previews the tax of an open basket and is
/// frozen into the payment, so the basket never shows 0.00 tax before payment.
/// </summary>
public sealed class TaxCalculator(IOptions<TaxSettings> settings)
{
    public Func<ILineItem, Money> Rule { get; } = TaxRule.From(settings.Value.ToRates());

    /// <summary>The tax of an open basket with today's rates, or the frozen tax of a paid one.</summary>
    public Money Preview(CounterTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction.PreviewTax(Rule);
    }
}
