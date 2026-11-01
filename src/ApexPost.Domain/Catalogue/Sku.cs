using ApexPost.Domain.Common;

namespace ApexPost.Domain.Catalogue;

/// <summary>A stock keeping unit: 3 letters, a dash and 4 digits, for example PKG-0001.</summary>
public sealed record Sku(string Value)
{
    private const int LetterCount = 3;
    private const int DigitCount = 4;
    private const char Separator = '-';

    public string Value { get; } = Validate(Value);

    public override string ToString() => Value;

    private static string Validate(string value)
    {
        var sku = value?.Trim().ToUpperInvariant() ?? string.Empty;
        var isValid = sku.Length == LetterCount + 1 + DigitCount
            && sku[..LetterCount].All(char.IsAsciiLetterUpper)
            && sku[LetterCount] == Separator
            && sku[(LetterCount + 1)..].All(char.IsAsciiDigit);
        return isValid ? sku : throw new DomainException("A SKU must look like PKG-0001.");
    }
}
