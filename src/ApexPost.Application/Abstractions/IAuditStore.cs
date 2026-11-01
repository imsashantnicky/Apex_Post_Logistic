using ApexPost.Application.DTOs;

namespace ApexPost.Application.Abstractions;

/// <summary>An append-only audit log: entries can be added and read, never changed or removed.</summary>
public interface IAuditStore
{
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken);

    /// <summary>Reads one page, newest entry first.</summary>
    Task<PagedResult<AuditEntry>> ReadAsync(PageRequest page, CancellationToken cancellationToken);
}
