using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using Couchbase.Lite;
using Couchbase.Lite.Query;

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

    // No extra "type" filter on the two queries below, so each one can be answered from its value index.
    private const string ByTrackingNumberQuery =
        "SELECT META().id AS id, trackingNumber, scanType, courierId, location, scannedAtUtc, notes, syncStatus " +
        "FROM _ WHERE trackingNumber = $trackingNumber ORDER BY scannedAtUtc";

    private const string BySyncStatusQuery =
        "SELECT META().id AS id, trackingNumber, scanType, courierId, location, scannedAtUtc, notes, syncStatus " +
        "FROM _ WHERE syncStatus = $syncStatus ORDER BY scannedAtUtc";

    private const string CountByScanTypeQuery =
        "SELECT scanType, COUNT(*) AS total FROM _ WHERE courierId = $courierId GROUP BY scanType ORDER BY scanType";

    private const string CountBySyncStatusQuery =
        "SELECT COUNT(*) AS total FROM _ WHERE syncStatus = $syncStatus";

    private const string TypeParameter = "type";
    private const string TrackingNumberParameter = "trackingNumber";
    private const string SyncStatusParameter = "syncStatus";
    private const string CourierIdParameter = "courierId";
    private const string IdColumn = "id";
    private const string TotalColumn = "total";

    // SELECT * returns the document body under the collection name.
    private const string BodyColumn = "_";

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
    /// <remarks>
    /// A key-value lookup by document id; no query runs. An invalid id returns <see langword="null"/>.
    /// </remarks>
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
        query.Parameters = new Parameters().SetString(TypeParameter, ScanDocumentMapper.TypeValue);
        using var results = query.Execute();

        var scans = new List<ShipmentScan>();
        foreach (var row in results)
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
    /// <remarks>
    /// The value is bound as <c>$trackingNumber</c>, never inserted into the query text, so an input such as
    /// <c>APX-00000000' OR '1'='1</c> is compared as one plain string and matches nothing.
    /// </remarks>
    public Task<IReadOnlyList<ShipmentScan>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trackingNumber);
        ct.ThrowIfCancellationRequested();
        using var query = _database.CreateQuery(ByTrackingNumberQuery);
        query.Parameters = new Parameters()
            .SetString(TrackingNumberParameter, trackingNumber.Trim().ToUpperInvariant());
        return Task.FromResult<IReadOnlyList<ShipmentScan>>(ReadScans(query, ct));
    }

    /// <inheritdoc />
    /// <remarks>Ordered oldest first by <c>scannedAtUtc</c>, so the outbox uploads in recording order.</remarks>
    public Task<IReadOnlyList<ShipmentScan>> GetPendingAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var query = _database.CreateQuery(BySyncStatusQuery);
        query.Parameters = PendingParameters();
        return Task.FromResult<IReadOnlyList<ShipmentScan>>(ReadScans(query, ct));
    }

    /// <inheritdoc />
    /// <remarks>Scan types the courier never used are left out. Ordered by scan type name.</remarks>
    public Task<IReadOnlyList<ScanTypeCount>> CountByScanTypeAsync(string courierId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(courierId);
        ct.ThrowIfCancellationRequested();
        using var query = _database.CreateQuery(CountByScanTypeQuery);
        query.Parameters = new Parameters().SetString(CourierIdParameter, courierId.Trim());
        using var results = query.Execute();

        var counts = new List<ScanTypeCount>();
        foreach (var row in results)
        {
            ct.ThrowIfCancellationRequested();
            counts.Add(new ScanTypeCount(
                ScanDocumentMapper.ParseEnum<ScanType>(row, ScanDocumentMapper.ScanTypeField),
                row.GetInt(TotalColumn)));
        }

        return Task.FromResult<IReadOnlyList<ScanTypeCount>>(counts);
    }

    /// <inheritdoc />
    /// <remarks>A single indexed count, cheap enough to run from a change listener after every save.</remarks>
    public Task<int> CountPendingAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var query = _database.CreateQuery(CountBySyncStatusQuery);
        query.Parameters = PendingParameters();
        using var results = query.Execute();
        return Task.FromResult(results.Select(row => row.GetInt(TotalColumn)).FirstOrDefault());
    }

    /// <summary>
    /// Returns the query plans of the tracking-number and pending queries, keyed by method name. The plans show
    /// whether <see cref="CourierDatabase.TrackingNumberIndexName"/> and
    /// <see cref="CourierDatabase.SyncStatusIndexName"/> are used.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExplainQueries()
    {
        using var byTrackingNumber = _database.CreateQuery(ByTrackingNumberQuery);
        using var pending = _database.CreateQuery(BySyncStatusQuery);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(GetByTrackingNumberAsync)] = byTrackingNumber.Explain(),
            [nameof(GetPendingAsync)] = pending.Explain(),
        };
    }

    private static Parameters PendingParameters() =>
        new Parameters().SetString(SyncStatusParameter, nameof(SyncStatus.Pending));

    private static List<ShipmentScan> ReadScans(IQuery query, CancellationToken ct)
    {
        using var results = query.Execute();
        var scans = new List<ShipmentScan>();
        foreach (var row in results)
        {
            ct.ThrowIfCancellationRequested();
            var id = row.GetString(IdColumn) ?? throw new FormatException("A query row has no id.");
            scans.Add(ScanDocumentMapper.FromDictionary(id, row));
        }

        return scans;
    }

    private Document? Find(string id) =>
        ScanIds.IsValid(id) ? _database.GetDefaultCollection().GetDocument(id) : null;
}
