using System.Globalization;

namespace ApexPost.Domain.Common;

/// <summary>A non-negative amount in one currency, rounded to 2 decimal places away from zero.</summary>
public sealed record Money(decimal Amount, string Currency = Money.DefaultCurrency) : IComparable<Money>
{
    public const string DefaultCurrency = "USD";
    private const int CurrencyCodeLength = 3;
    private const int DecimalPlaces = 2;

    public decimal Amount { get; } = ValidAmount(Amount);

    public string Currency { get; } = ValidCurrency(Currency);

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static Money operator +(Money left, Money right) =>
        new(left.Amount + SameCurrency(left, right).Amount, left.Currency);

    public static Money operator -(Money left, Money right) =>
        new(left.Amount - SameCurrency(left, right).Amount, left.Currency);

    public static Money operator *(Money money, int quantity) => new(money.Amount * quantity, money.Currency);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public int CompareTo(Money? other) =>
        other is null ? 1 : Amount.CompareTo(SameCurrency(this, other).Amount);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");

    private static decimal ValidAmount(decimal amount) =>
        amount >= 0m
            ? Math.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero)
            : throw new DomainException("A money amount cannot be negative.");

    private static string ValidCurrency(string currency)
    {
        var code = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        return code.Length == CurrencyCodeLength && code.All(char.IsAsciiLetterUpper)
            ? code
            : throw new DomainException("A currency must be a 3-letter code.");
    }

    private static Money SameCurrency(Money left, Money right) =>
        left.Currency == right.Currency
            ? right
            : throw new DomainException($"Cannot combine {left.Currency} with {right.Currency}.");
}
