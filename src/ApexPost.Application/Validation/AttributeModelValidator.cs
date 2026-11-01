using System.Collections.Concurrent;
using System.Reflection;
using ApexPost.Application.Exceptions;

namespace ApexPost.Application.Validation;

/// <summary>
/// Checks the <see cref="ValidationRuleAttribute"/>s on a model's public properties. Reading attributes with
/// reflection is slow, so the rules of each type are read once and cached.
/// </summary>
public sealed class AttributeModelValidator
{
    private readonly ConcurrentDictionary<Type, PropertyRules[]> _rulesByType = new();

    /// <summary>The number of model types whose rules are cached.</summary>
    public int CachedTypeCount => _rulesByType.Count;

    /// <summary>Returns the error messages per property; empty when the model is valid.</summary>
    public IReadOnlyDictionary<string, string[]> Validate(object model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var property in _rulesByType.GetOrAdd(model.GetType(), ReadRules))
        {
            var value = property.Property.GetValue(model);
            string[] messages = [.. property.Rules.Select(rule => rule.Check(property.Property.Name, value))
                .OfType<string>()];
            if (messages.Length > 0)
            {
                errors[property.Property.Name] = messages;
            }
        }

        return errors;
    }

    /// <summary>Throws <see cref="ValidationFailedException"/> when the model has errors.</summary>
    public void EnsureValid(object model)
    {
        var errors = Validate(model);
        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }
    }

    private static PropertyRules[] ReadRules(Type type) =>
    [
        .. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Select(property => new PropertyRules(
                property,
                [.. property.GetCustomAttributes<ValidationRuleAttribute>(inherit: true)]))
            .Where(property => property.Rules.Length > 0),
    ];

    private sealed record PropertyRules(PropertyInfo Property, ValidationRuleAttribute[] Rules);
}
