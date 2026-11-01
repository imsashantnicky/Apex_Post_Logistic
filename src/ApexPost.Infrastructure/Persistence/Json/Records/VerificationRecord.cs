using ApexPost.Domain.Verification;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>An identity check; only the masked number (for example ****4567) is ever stored.</summary>
public sealed record VerificationRecord
{
    public required DocumentType DocumentType { get; init; }

    public required string MaskedNumber { get; init; }

    public required VerificationResult Result { get; init; }

    public required DateTimeOffset VerifiedAt { get; init; }
}
