using ApexPost.Domain.Common;

namespace ApexPost.Domain.Rules;

/// <summary>A money order costs 2 % of the amount, with a minimum fee of 2.00.</summary>
public static class MoneyOrderFeeRule
{
    public const decimal MinimumFee = 2.00m;
    public const decimal FeePercent = 2m;

    public static Money FeeFor(Money amount)
    {
        var percentFee = Guard.Required(amount, nameof(amount)).Percent(FeePercent);
        var minimumFee = new Money(MinimumFee, amount.Currency);
        return percentFee > minimumFee ? percentFee : minimumFee;
    }
}
