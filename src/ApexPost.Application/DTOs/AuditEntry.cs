using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.DTOs;

/// <summary>
/// One audited action: who (staff, role, branch), what (event, controller, action, key route values),
/// the outcome, how long it took and the correlation ID. Never holds passwords, document numbers or keys.
/// </summary>
public sealed record AuditEntry(
    DateTimeOffset At,
    string Event,
    Guid? StaffId,
    StaffRole? Role,
    BranchCode? Branch,
    string Controller,
    string Action,
    IReadOnlyDictionary<string, string> RouteValues,
    int StatusCode,
    long DurationMs,
    string CorrelationId);
