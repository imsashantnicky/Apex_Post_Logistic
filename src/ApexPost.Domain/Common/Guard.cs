using System.Globalization;

namespace ApexPost.Domain.Common;

internal static class Guard
{
    public static string NotBlank(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new DomainException(
                string.Create(CultureInfo.InvariantCulture, $"{name} must be at most {maxLength} characters."));
    }

    public static int Positive(int value, string name) =>
        value > 0 ? value : throw new DomainException($"{name} must be greater than zero.");

    public static decimal Positive(decimal value, string name) =>
        value > 0m ? value : throw new DomainException($"{name} must be greater than zero.");

    public static Guid NotEmpty(Guid value, string name) =>
        value != Guid.Empty ? value : throw new DomainException($"{name} is required.");

    public static TEnum Defined<TEnum>(TEnum value, string name)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new DomainException($"{name} '{value}' is not valid.");
}
