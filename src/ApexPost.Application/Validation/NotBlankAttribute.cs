namespace ApexPost.Application.Validation;

/// <summary>The value is required; text must contain more than white space.</summary>
public sealed class NotBlankAttribute : ValidationRuleAttribute
{
    public override string? Check(string field, object? value) => value switch
    {
        null => $"{field} is required.",
        string text when string.IsNullOrWhiteSpace(text) => $"{field} is required.",
        _ => null,
    };
}
