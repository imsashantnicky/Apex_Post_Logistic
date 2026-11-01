using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Extensions;

/// <summary>Summary properties of a basket used by the counter screen and the sales report.</summary>
public static class BasketSummaryExtensions
{
    private const string Separator = " – ";

    extension(CounterTransaction transaction)
    {
        /// <summary>The total of the retail goods.</summary>
        public Money RetailTotal => Sum(transaction, line => line is RetailLineItem);

        /// <summary>The total of shipping and money orders.</summary>
        public Money ServicesTotal => Sum(transaction, line => line is not RetailLineItem);

        /// <summary>The latest identity check, for example "Passport ****4567 – Passed"; null when none.</summary>
        public string? VerificationSummary => transaction.Verifications is [.., var latest]
            ? $"{latest.DocumentType} {latest.MaskedNumber}{Separator}{latest.Result}"
            : null;
    }

    private static Money Sum(CounterTransaction transaction, Func<ILineItem, bool> include) =>
        transaction.Lines
            .Where(include)
            .Aggregate(Money.Zero(transaction.Subtotal.Currency), (total, line) => total + line.Total);
}
