using ApexPost.Domain.Verification;

namespace ApexPost.Application.Extensions;

public static class IdentityVerificationExtensions
{
    private const string Separator = " – ";

    extension(IdentityVerification verification)
    {
        /// <summary>Only the document type, the masked number and the result.</summary>
        public string Summary =>
            $"{verification.DocumentType} {verification.MaskedNumber}{Separator}{verification.Result}";
    }
}
