using System.Text.Json;
using ApexPost.Application.DTOs;
using ApexPost.Infrastructure.Persistence.Mappers;
using ApexPost.Infrastructure.Persistence.Records;
using ApexPost.Web.Middleware;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Support;

/// <summary>Reads back what a request left in the audit table.</summary>
internal static class AuditTrail
{
    public static async Task<AuditEntry> EntryForAsync(ApexFactory factory, HttpResponseMessage response, string name)
    {
        var correlationId = Assert.Single(response.Headers.GetValues(ApexHeaders.CorrelationId));
        var entries = await EntriesAsync(factory);
        return Assert.Single(entries, entry => entry.CorrelationId == correlationId && entry.Event == name);
    }

    /// <summary>The audit rows exactly as stored, as text.</summary>
    public static async Task<string> RawRowsAsync(ApexFactory factory)
    {
        await using var database = factory.OpenDatabase();
        var rows = await database.Set<AuditEntryRecord>().AsNoTracking().ToListAsync();
        return JsonSerializer.Serialize(rows);
    }

    /// <summary>The identity checks exactly as stored, as text.</summary>
    public static async Task<string> RawChecksAsync(ApexFactory factory)
    {
        await using var database = factory.OpenDatabase();
        var rows = await database.Set<IdentityVerificationRecord>().AsNoTracking().ToListAsync();
        return JsonSerializer.Serialize(rows);
    }

    public static async Task<IReadOnlyList<AuditEntry>> EntriesAsync(ApexFactory factory)
    {
        await using var database = factory.OpenDatabase();
        var rows = await database.Set<AuditEntryRecord>()
            .AsNoTracking()
            .OrderByDescending(record => record.At)
            .ThenByDescending(record => record.Id)
            .ToListAsync();
        return [.. rows.Select(AuditEntryMapper.ToEntry)];
    }
}
