using System.Text.RegularExpressions;

namespace ApexPost.Application.Validation;

/// <summary>Text, when present, matches a regular expression; the example is shown in the error message.</summary>
public sealed class PatternAttribute(string pattern, string example) : ValidationRuleAttribute
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private readonly Regex _regex = new(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, MatchTimeout);

    public string Pattern { get; } = pattern;

    public string Example { get; } = example;

    public override string? Check(string field, object? value) =>
        value is string text && !string.IsNullOrWhiteSpace(text) && !_regex.IsMatch(text.Trim())
            ? $"{field} must look like {Example}."
            : null;
}
