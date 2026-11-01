using ApexPost.Domain.Common;

namespace ApexPost.Application.Extensions;

public static class MoneyCollectionExtensions
{
    extension(IEnumerable<Money> amounts)
    {
        /// <summary>Adds up the amounts; an empty list gives zero in the default currency.</summary>
        public Money Total() => amounts.Aggregate(Money.Zero(), (total, amount) => total + amount);
    }
}
