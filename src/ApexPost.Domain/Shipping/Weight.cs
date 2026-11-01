using System.Globalization;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

/// <summary>A positive weight stored in whole grams.</summary>
public sealed record Weight(int Grams) : IComparable<Weight>
{
    private const decimal GramsPerKilogram = 1000m;

    public int Grams { get; } = Guard.Positive(Grams, "Weight");

    public decimal Kilograms => Grams / GramsPerKilogram;

    public static Weight FromKilograms(decimal kilograms)
    {
        var grams = Math.Round(kilograms * GramsPerKilogram, MidpointRounding.AwayFromZero);
        return grams is > 0m and <= int.MaxValue
            ? new Weight((int)grams)
            : throw new DomainException("Weight must be greater than zero.");
    }

    public static bool operator <(Weight left, Weight right) => left.CompareTo(right) < 0;

    public static bool operator >(Weight left, Weight right) => left.CompareTo(right) > 0;

    public static bool operator <=(Weight left, Weight right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Weight left, Weight right) => left.CompareTo(right) >= 0;

    public int CompareTo(Weight? other) => other is null ? 1 : Grams.CompareTo(other.Grams);

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Kilograms:0.000} kg");
}
