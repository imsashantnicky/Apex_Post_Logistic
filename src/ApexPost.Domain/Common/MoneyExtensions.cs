namespace ApexPost.Domain.Common;

public static class MoneyExtensions
{
    private const decimal CashStep = 0.05m;
    private const decimal Hundred = 100m;

    extension(Money money)
    {
        public bool IsPositive => money.Amount > 0m;

        /// <summary>Rounds to the nearest 0.05, the smallest coin step accepted at the counter.</summary>
        public Money RoundedForCash =>
            new(Math.Round(money.Amount / CashStep, MidpointRounding.AwayFromZero) * CashStep, money.Currency);

        public Money Percent(decimal percent) => new(money.Amount * percent / Hundred, money.Currency);
    }
}
