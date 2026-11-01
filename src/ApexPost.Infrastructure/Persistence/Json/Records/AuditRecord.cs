using ApexPost.Domain.Personnel;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>One line of audit.jsonl. It has no version because audit entries are never updated.</summary>
public sealed record AuditRecord
{
    public required DateTimeOffset At { get; init; }

    public required string Event { get; init; }

    public Guid? StaffId { get; init; }

    public StaffRole? Role { get; init; }

    public string? Branch { get; init; }

    public required string Controller { get; init; }

    public required string Action { get; init; }

    public required IReadOnlyDictionary<string, string> RouteValues { get; init; }

    public required int StatusCode { get; init; }

    public required long DurationMs { get; init; }

    public required string CorrelationId { get; init; }
}
