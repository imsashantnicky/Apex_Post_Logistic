using ApexPost.Application.DTOs;

namespace ApexPost.Web.ViewModels;

/// <summary>One audit entry as plain text and numbers, for the list and the JSON export.</summary>
public sealed record AuditRow(
    DateTimeOffset At,
    string Event,
    Guid? StaffId,
    string? Role,
    string? Branch,
    string Controller,
    string Action,
    IReadOnlyDictionary<string, string> RouteValues,
    int StatusCode,
    long DurationMs,
    string CorrelationId)
{
    public string Values => string.Join(", ", RouteValues.Select(pair => $"{pair.Key}={pair.Value}"));

    public static AuditRow From(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new AuditRow(
            entry.At,
            entry.Event,
            entry.StaffId,
            entry.Role?.ToString(),
            entry.Branch?.Value,
            entry.Controller,
            entry.Action,
            entry.RouteValues,
            entry.StatusCode,
            entry.DurationMs,
            entry.CorrelationId);
    }
}
