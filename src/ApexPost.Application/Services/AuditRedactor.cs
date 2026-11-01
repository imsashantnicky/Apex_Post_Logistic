using System.Text.RegularExpressions;
using ApexPost.Domain.Verification;

namespace ApexPost.Application.Services;

/// <summary>
/// Cleans the values of an audit entry before they reach the store. A value under a sensitive name is dropped
/// whole. Any other text can still hold what someone typed into a URL, so card-like numbers are removed, and
/// anything shaped like a document number keeps only its last 4 characters. GUIDs are record keys and stay.
/// </summary>
public static partial class AuditRedactor
{
    public const string Redacted = "[redacted]";

    private static readonly string[] SensitiveNames =
    [
        "password", "passcode", "secret", "token", "apikey", "authorization", "document", "card", "cvv", "cvc",
    ];

    public static IReadOnlyDictionary<string, string> Redact(IEnumerable<KeyValuePair<string, string>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ToDictionary(
            pair => pair.Key,
            pair => IsSensitive(pair.Key) ? Redacted : CleanValue(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSensitive(string name)
    {
        var letters = string.Concat(name.Where(char.IsAsciiLetter));
        return SensitiveNames.Any(sensitive => letters.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanValue(string value) => Guid.TryParse(value, out _)
        ? value
        : DocumentLike().Replace(CardLike().Replace(value, Redacted), MaskToken);

    private static string MaskToken(Match token) =>
        token.Value.Any(char.IsAsciiDigit)
            ? IdentityVerification.Mask(token.Value)
            : token.Value;

    [GeneratedRegex(@"\d(?:[ -]?\d){11,18}")]
    private static partial Regex CardLike();

    [GeneratedRegex("[A-Za-z0-9]{6,}")]
    private static partial Regex DocumentLike();
}
