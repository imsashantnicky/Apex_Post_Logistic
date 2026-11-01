using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;

namespace ApexPost.Domain.Rules;

/// <summary>Builds the tax delegate that previews basket tax and freezes it at payment.</summary>
public static class TaxRule
{
    public static Func<ILineItem, Money> From(TaxRates rates)
    {
        Guard.Required(rates, nameof(rates));
        return line => line.Total.Percent(rates.PercentFor(line.TaxCategory));
    }
}
