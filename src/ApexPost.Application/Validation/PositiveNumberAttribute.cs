namespace ApexPost.Application.Validation;

/// <summary>A number, when present, is greater than zero.</summary>
public sealed class PositiveNumberAttribute : ValidationRuleAttribute
{
    public override string? Check(string field, object? value)
    {
        var isPositive = value switch
        {
            null => true,
            int number => number > 0,
            long number => number > 0,
            decimal number => number > 0m,
            double number => number > 0d,
            _ => false,
        };
        return isPositive ? null : $"{field} must be greater than zero.";
    }
}
