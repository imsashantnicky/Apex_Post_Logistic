using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Services;

/// <summary>Reading the audit log, for auditors and administrators only.</summary>
public sealed class AuditService(IAuditStore store, ICurrentUser user)
{
    public Task<PagedResult<AuditEntry>> ReadAsync(PageRequest page, CancellationToken cancellationToken)
    {
        RequireViewer();
        return store.ReadAsync(page, cancellationToken);
    }

    /// <summary>Every entry, newest first.</summary>
    public async Task<IReadOnlyList<AuditEntry>> ExportAsync(CancellationToken cancellationToken)
    {
        RequireViewer();
        var entries = new List<AuditEntry>();
        var number = 1;
        PagedResult<AuditEntry> page;
        do
        {
            page = await store.ReadAsync(new PageRequest(number++, PageRequest.MaximumSize), cancellationToken)
                .ConfigureAwait(false);
            entries.AddRange(page.Items);
        }
        while (page.HasNext);

        return entries;
    }

    private void RequireViewer() => user.RequireRole(StaffRole.Auditor, StaffRole.Administrator);
}
