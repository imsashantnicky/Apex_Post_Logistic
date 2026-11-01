using System.Text.Json;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Infrastructure.Persistence.Json.Mapping;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Auditing;

/// <summary>
/// Append-only audit log in audit.jsonl: one JSON object per line, added at the end, never rewritten.
/// Appends and reads share the file's lock in <see cref="JsonFileStore"/>.
/// </summary>
public sealed class JsonLinesAuditStore(JsonFileStore files) : IAuditStore
{
    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        var line = JsonSerializer.Serialize(AuditMapping.ToRecord(entry), JsonDefaults.Lines) + Environment.NewLine;
        return files.WithLockAsync(
            DataFiles.Audit,
            async path =>
            {
                Directory.CreateDirectory(files.DataFolder);
                await File.AppendAllTextAsync(path, line, cancellationToken).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    public async Task<PagedResult<AuditEntry>> ReadAsync(PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var lines = await files.WithLockAsync(
            DataFiles.Audit,
            path => File.Exists(path)
                ? File.ReadAllLinesAsync(path, cancellationToken)
                : Task.FromResult(Array.Empty<string>()),
            cancellationToken).ConfigureAwait(false);
        AuditEntry[] newestFirst =
        [
            .. lines
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Line))
                .Reverse()
                .Select(entry => Parse(entry.Line, entry.Number)),
        ];
        return newestFirst.ToPage(page);
    }

    private static AuditEntry Parse(string line, int lineNumber)
    {
        try
        {
            var record = JsonSerializer.Deserialize<AuditRecord>(line, JsonDefaults.Lines)
                ?? throw new JsonException("The line holds null.");
            return AuditMapping.ToEntry(record);
        }
        catch (JsonException exception)
        {
            throw new DataFileException(
                $"Line {lineNumber} of '{DataFiles.Audit}' is malformed: {exception.Message}",
                exception);
        }
    }
}
