namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>The PBKDF2 hash of one staff member's password, kept in its own file.</summary>
public sealed record CredentialRecord : IVersionedRecord
{
    public required Guid StaffId { get; init; }

    public required string PasswordHash { get; init; }

    public required int Version { get; init; }
}
