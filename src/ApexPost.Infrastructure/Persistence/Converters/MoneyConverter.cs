using ApexPost.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="Money"/> as its amount; the database holds one currency (USD).</summary>
public sealed class MoneyConverter()
    : ValueConverter<Money, decimal>(money => money.Amount, amount => new Money(amount, Money.DefaultCurrency));
