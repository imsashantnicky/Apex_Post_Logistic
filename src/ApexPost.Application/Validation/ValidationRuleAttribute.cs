namespace ApexPost.Application.Validation;

/// <summary>Base for the input rules read by <see cref="AttributeModelValidator"/>.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public abstract class ValidationRuleAttribute : Attribute
{
    /// <summary>Returns an error message for the field, or null when the value is valid.</summary>
    public abstract string? Check(string field, object? value);
}
