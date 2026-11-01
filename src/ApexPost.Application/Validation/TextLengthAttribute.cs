using System.Globalization;

namespace ApexPost.Application.Validation;

/// <summary>Text, when present, has between <see cref="Minimum"/> and <see cref="Maximum"/> characters.</summary>
public sealed class TextLengthAttribute(int maximum) : ValidationRuleAttribute
{
    public int Maximum { get; } = maximum;

    public int Minimum { get; init; }

    public override string? Check(string field, object? value) => value switch
    {
        string text when text.Trim().Length > Maximum =>
            string.Create(CultureInfo.InvariantCulture, $"{field} must be at most {Maximum} characters."),
        string text when text.Trim().Length < Minimum =>
            string.Create(CultureInfo.InvariantCulture, $"{field} must be at least {Minimum} characters."),
        _ => null,
    };
}
