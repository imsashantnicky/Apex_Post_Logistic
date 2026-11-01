namespace ApexPost.Infrastructure.Persistence.Records;

/// <summary>One audited action as stored, append-only.</summary>
public sealed class AuditEntryRecord
{
    public long Id { get; set; }

    public DateTimeOffset At { get; set; }

    public string Event { get; set; } = string.Empty;

    public Guid? StaffId { get; set; }

    public string? Role { get; set; }

    public string? BranchCode { get; set; }

    public string Controller { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string RouteValues { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public long DurationMs { get; set; }

    public string CorrelationId { get; set; } = string.Empty;
}
