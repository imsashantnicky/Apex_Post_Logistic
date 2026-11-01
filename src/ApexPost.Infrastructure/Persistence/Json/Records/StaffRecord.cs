using ApexPost.Domain.Personnel;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>A staff row. The password hash lives in <see cref="CredentialRecord"/>, never here.</summary>
public sealed record StaffRecord : IVersionedRecord
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Email { get; init; }

    public required StaffRole Role { get; init; }

    public required string HomeBranch { get; init; }

    public required bool IsActive { get; init; }

    public required int Version { get; init; }
}
