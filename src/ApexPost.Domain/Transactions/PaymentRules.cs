using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

/// <summary>The configurable rules applied when a basket is paid.</summary>
public sealed record PaymentRules(Money VerificationThreshold, Func<ILineItem, Money> TaxRule)
{
    public Money VerificationThreshold { get; } = Guard.Required(VerificationThreshold, "Verification threshold");

    public Func<ILineItem, Money> TaxRule { get; } = Guard.Required(TaxRule, "Tax rule");
}
