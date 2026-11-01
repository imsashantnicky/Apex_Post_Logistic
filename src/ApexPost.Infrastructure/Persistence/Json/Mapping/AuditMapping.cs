using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

public static class AuditMapping
{
    public static AuditRecord ToRecord(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new()
        {
            At = entry.At,
            Event = entry.Event,
            StaffId = entry.StaffId,
            Role = entry.Role,
            Branch = entry.Branch?.Value,
            Controller = entry.Controller,
            Action = entry.Action,
            RouteValues = entry.RouteValues,
            StatusCode = entry.StatusCode,
            DurationMs = entry.DurationMs,
            CorrelationId = entry.CorrelationId,
        };
    }

    public static AuditEntry ToEntry(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(
            record.At,
            record.Event,
            record.StaffId,
            record.Role,
            record.Branch is { } branch ? new BranchCode(branch) : null,
            record.Controller,
            record.Action,
            record.RouteValues,
            record.StatusCode,
            record.DurationMs,
            record.CorrelationId);
    }
}
