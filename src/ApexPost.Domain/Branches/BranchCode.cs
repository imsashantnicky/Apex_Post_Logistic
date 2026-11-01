using ApexPost.Domain.Common;

namespace ApexPost.Domain.Branches;

/// <summary>A branch code: "BR" followed by 3 digits, for example BR001.</summary>
public sealed record BranchCode(string Value)
{
    private const string Prefix = "BR";
    private const int DigitCount = 3;

    public string Value { get; } = Validate(Value);

    public override string ToString() => Value;

    private static string Validate(string value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? string.Empty;
        var isValid = code.Length == Prefix.Length + DigitCount
            && code.StartsWith(Prefix, StringComparison.Ordinal)
            && code[Prefix.Length..].All(char.IsAsciiDigit);
        return isValid ? code : throw new DomainException("A branch code must look like BR001.");
    }
}
