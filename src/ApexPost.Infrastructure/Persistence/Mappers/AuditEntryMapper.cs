using System.Text.Json;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Persistence.Records;

namespace ApexPost.Infrastructure.Persistence.Mappers;

/// <summary>Maps an audit entry to its append-only row and back; the route values are kept as JSON text.</summary>
public static class AuditEntryMapper
{
    public static AuditEntryRecord ToRecord(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new AuditEntryRecord
        {
            At = entry.At,
            Event = entry.Event,
            StaffId = entry.StaffId,
            Role = entry.Role?.ToString(),
            BranchCode = entry.Branch?.Value,
            Controller = entry.Controller,
            Action = entry.Action,
            RouteValues = JsonSerializer.Serialize(entry.RouteValues),
            StatusCode = entry.StatusCode,
            DurationMs = entry.DurationMs,
            CorrelationId = entry.CorrelationId,
        };
    }

    public static AuditEntry ToEntry(AuditEntryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new AuditEntry(
            record.At,
            record.Event,
            record.StaffId,
            record.Role is null ? null : Enum.Parse<StaffRole>(record.Role),
            record.BranchCode is null ? null : new BranchCode(record.BranchCode),
            record.Controller,
            record.Action,
            JsonSerializer.Deserialize<Dictionary<string, string>>(record.RouteValues) ?? [],
            record.StatusCode,
            record.DurationMs,
            record.CorrelationId);
    }
}
