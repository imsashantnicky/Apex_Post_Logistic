using ApexPost.Domain.Common;

namespace ApexPost.Domain.Verification;

/// <summary>
/// The outcome of an identity check. Only the masked document number (for example ****1234) is kept;
/// the full number never enters this type.
/// </summary>
public sealed record IdentityVerification(
    DocumentType DocumentType,
    string MaskedNumber,
    VerificationResult Result,
    DateTimeOffset VerifiedAt)
{
    public const int MinimumDocumentNumberLength = 6;
    public const string MaskPrefix = "****";
    public const int VisibleCharacters = 4;

    public DocumentType DocumentType { get; } = Guard.Defined(DocumentType, nameof(DocumentType));

    public string MaskedNumber { get; } = ValidMask(MaskedNumber);

    public VerificationResult Result { get; } = Guard.Defined(Result, nameof(Result));

    public bool IsPassed => Result == VerificationResult.Passed;

    public static IdentityVerification FromDocument(
        DocumentType documentType,
        string documentNumber,
        VerificationResult result,
        DateTimeOffset verifiedAt) =>
        new(documentType, Mask(documentNumber), result, verifiedAt);

    /// <summary>Keeps the last 4 characters. The error message never repeats the number.</summary>
    public static string Mask(string documentNumber)
    {
        var number = documentNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        return number.Length >= MinimumDocumentNumberLength && number.All(char.IsAsciiLetterOrDigit)
            ? MaskPrefix + number[^VisibleCharacters..]
            : throw new DomainException("The document number is not valid.");
    }

    private static string ValidMask(string maskedNumber)
    {
        var mask = maskedNumber ?? string.Empty;
        var isValid = mask.Length == MaskPrefix.Length + VisibleCharacters
            && mask.StartsWith(MaskPrefix, StringComparison.Ordinal)
            && mask[MaskPrefix.Length..].All(char.IsAsciiLetterOrDigit);
        return isValid ? mask : throw new DomainException("Only a masked document number can be stored.");
    }
}
