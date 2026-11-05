using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using Couchbase.Lite;

namespace ApexPost.Courier.Offline.Scans;

/// <summary>
/// Stores scans as JSON documents in the default collection of <see cref="CourierDatabase"/>.
/// Reads by id are key-value lookups; lists use parameterised SQL++. Couchbase Lite is synchronous and
/// thread-safe, so each method does its work inline and returns a completed task.
/// </summary>
public sealed class CouchbaseScanRepository : IScanRepository, IScanQueries
{
    private const string ListAllQuery =
        "SELECT META().id AS id, * FROM _ WHERE type = $type ORDER BY scannedAtUtc, META().id";

    private const string TypeParameter = "type";
    private const string IdColumn = "id";

    // SELECT * returns the document body under the collection name.
    private const string BodyColumn = "_";

    private const string QueriesNotReady = "Scan queries are added in the next step.";

    private readonly CourierDatabase _database;

    /// <summary>Creates a repository over an open database. The caller keeps ownership of the database.</summary>
    public CouchbaseScanRepository(CourierDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The scan id is not of the form <c>scan::{Guid}</c>.</exception>
    /// <exception cref="InvalidOperationException">A scan with the same id already exists.</exception>
    public Task CreateAsync(ShipmentScan scan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scan);
        ct.ThrowIfCancellationRequested();
        if (!ScanIds.IsValid(scan.Id))
        {
            throw new ArgumentException($"Scan id must look like '{ScanIds.Prefix}{{guid}}'.", nameof(scan));
        }

        var collection = _database.GetDefaultCollection();
        if (collection.GetDocument(scan.Id) is not null
            || !collection.Save(ScanDocumentMapper.ToDocument(scan), ConcurrencyControl.FailOnConflict))
        {
            throw new InvalidOperationException($"A scan with id '{scan.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>A key-value lookup by document id; no query runs. An invalid id returns <see langword="null"/>.</remarks>
    public Task<ShipmentScan?> GetByIdAsync(string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var document = Find(id);
        return Task.FromResult(document is null ? null : ScanDocumentMapper.FromDocument(document));
    }

    /// <inheritdoc />
    /// <remarks>
    /// An edited scan must be uploaded again, so it is always saved as <see cref="SyncStatus.Pending"/>,
    /// even when it was <see cref="SyncStatus.Synced"/> before.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">No scan with this id exists.</exception>
    public Task UpdateAsync(ShipmentScan scan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scan);
        ct.ThrowIfCancellationRequested();
        var document = Find(scan.Id) ?? throw new KeyNotFoundException($"No scan with id '{scan.Id}' exists.");

        var mutable = ScanDocumentMapper.Write(document.ToMutable(), scan);
        mutable.SetString(ScanDocumentMapper.SyncStatusField, nameof(SyncStatus.Pending));
        _database.GetDefaultCollection().Save(mutable);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var document = Find(id);
        if (document is null)
        {
            return Task.FromResult(false);
        }

        _database.GetDefaultCollection().Delete(document);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    /// <remarks>Ordered oldest first by <c>scannedAtUtc</c>.</remarks>
    public Task<IReadOnlyList<ShipmentScan>> ListAllAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var query = _database.CreateQuery(ListAllQuery);
        query.Parameters.SetString(TypeParameter, ScanDocumentMapper.TypeValue);

        var scans = new List<ShipmentScan>();
        foreach (var row in query.Execute())
        {
            ct.ThrowIfCancellationRequested();
            var id = row.GetString(IdColumn) ?? throw new FormatException("A query row has no id.");
            var body = row.GetDictionary(BodyColumn) ?? throw new FormatException($"Row '{id}' has no body.");
            scans.Add(ScanDocumentMapper.FromDictionary(id, body));
        }

        return Task.FromResult<IReadOnlyList<ShipmentScan>>(scans);
    }

    /// <summary>Changes only the sync status of a stored scan. Used by the outbox after an upload attempt.</summary>
    /// <returns><see langword="false"/> when no scan with this id exists.</returns>
    /// <exception cref="ArgumentException"><paramref name="status"/> is not a defined value.</exception>
    public Task<bool> UpdateSyncStatusAsync(string id, SyncStatus status, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException($"Unknown {nameof(SyncStatus)} value {status}.", nameof(status));
        }

        var document = Find(id);
        if (document is null)
        {
            return Task.FromResult(false);
        }

        var mutable = document.ToMutable();
        mutable.SetString(ScanDocumentMapper.SyncStatusField, status.ToString());
        _database.GetDefaultCollection().Save(mutable);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ShipmentScan>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken ct) =>
        throw new NotSupportedException(QueriesNotReady);

    /// <inheritdoc />
    public Task<IReadOnlyList<ShipmentScan>> GetPendingAsync(CancellationToken ct) =>
        throw new NotSupportedException(QueriesNotReady);

    /// <inheritdoc />
    public Task<IReadOnlyList<ScanTypeCount>> CountByScanTypeAsync(string courierId, CancellationToken ct) =>
        throw new NotSupportedException(QueriesNotReady);

    /// <inheritdoc />
    public Task<int> CountPendingAsync(CancellationToken ct) =>
        throw new NotSupportedException(QueriesNotReady);

    private Document? Find(string id) =>
        ScanIds.IsValid(id) ? _database.GetDefaultCollection().GetDocument(id) : null;
}
