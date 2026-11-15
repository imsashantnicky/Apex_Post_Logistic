using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.Domain.Ports;

/// <summary>
/// Changes only the sync status of a stored scan. The outbox is the only caller (the offline rule: only the outbox
/// changes <see cref="SyncStatus"/>). A port of its own so the outbox can be tested without a database.
/// </summary>
public interface IScanSyncStatusUpdater
{
    /// <summary>Saves <paramref name="status"/> on the scan with this id.</summary>
    /// <param name="id">The scan id, <c>scan::{Guid}</c>.</param>
    /// <param name="status">The new sync status.</param>
    /// <param name="ct">Cancels the update.</param>
    /// <returns><see langword="false"/> when no scan with this id exists.</returns>
    Task<bool> UpdateSyncStatusAsync(string id, SyncStatus status, CancellationToken ct);
}
