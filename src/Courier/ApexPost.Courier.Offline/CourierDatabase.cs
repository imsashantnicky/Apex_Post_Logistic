using Couchbase.Lite;
using Couchbase.Lite.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.Offline;

/// <summary>
/// Owns the lifecycle of the local Couchbase Lite database that holds courier scans.
/// Opening creates the value indexes; disposing removes every listener token first and then closes the database.
/// </summary>
public sealed partial class CourierDatabase : IDisposable
{
    /// <summary>Name of the Couchbase Lite database on the device.</summary>
    public const string DatabaseName = "apexpost_courier";

    /// <summary>Folder under <see cref="AppContext.BaseDirectory"/> used when no directory is given.</summary>
    public const string DefaultFolderName = "CouchbaseData";

    /// <summary>Value index on the <c>trackingNumber</c> property.</summary>
    public const string TrackingNumberIndexName = "idx_scan_trackingNumber";

    /// <summary>Value index on the <c>syncStatus</c> property.</summary>
    public const string SyncStatusIndexName = "idx_scan_syncStatus";

    private readonly Lock _gate = new();
    private readonly List<ListenerToken> _listenerTokens = [];
    private readonly ILogger<CourierDatabase> _logger;
    private readonly Database _database;
    private bool _disposed;

    /// <summary>Opens (or creates) the database and makes sure the value indexes exist.</summary>
    /// <param name="directory">Folder for the database files; defaults to <c>BaseDirectory/CouchbaseData</c>.</param>
    /// <param name="logger">Optional logger; lifecycle messages are dropped when it is <see langword="null"/>.</param>
    public CourierDatabase(string? directory = null, ILogger<CourierDatabase>? logger = null)
    {
        _logger = logger ?? NullLogger<CourierDatabase>.Instance;
        Directory = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(AppContext.BaseDirectory, DefaultFolderName)
            : directory;

        _database = new Database(DatabaseName, new DatabaseConfiguration { Directory = Directory });
        EnsureIndexes(_database.GetDefaultCollection());
        LogDatabaseOpened(_logger, Directory);
    }

    /// <summary>Folder that holds the database files.</summary>
    public string Directory { get; }

    /// <summary>Returns the default collection, where scan documents are stored.</summary>
    /// <exception cref="ObjectDisposedException">The database was disposed.</exception>
    public Collection GetDefaultCollection()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _database.GetDefaultCollection();
        }
    }

    /// <summary>Creates a SQL++ query. Values must be passed through <see cref="IQuery.Parameters"/>.</summary>
    /// <exception cref="ObjectDisposedException">The database was disposed.</exception>
    internal IQuery CreateQuery(string sql)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _database.CreateQuery(sql);
        }
    }

    /// <summary>Returns the names of the indexes on the default collection.</summary>
    /// <exception cref="ObjectDisposedException">The database was disposed.</exception>
    public IReadOnlyList<string> GetIndexNames() => [.. GetDefaultCollection().GetIndexes()];

    /// <summary>Keeps a listener token so that <see cref="Dispose"/> removes it before closing the database.</summary>
    /// <exception cref="ObjectDisposedException">The database was disposed.</exception>
    public void RegisterListenerToken(ListenerToken token)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _listenerTokens.Add(token);
        }
    }

    /// <summary>Removes every registered listener token, then closes the database. Safe to call more than once.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var token in _listenerTokens)
            {
                token.Remove();
                LogListenerTokenRemoved(_logger);
            }

            _listenerTokens.Clear();
            _database.Close();
            _database.Dispose();
            LogDatabaseClosed(_logger, Directory);
        }
    }

    private static void EnsureIndexes(Collection collection)
    {
        var existing = collection.GetIndexes();
        if (!existing.Contains(TrackingNumberIndexName))
        {
            collection.CreateIndex(TrackingNumberIndexName, new ValueIndexConfiguration("trackingNumber"));
        }

        if (!existing.Contains(SyncStatusIndexName))
        {
            collection.CreateIndex(SyncStatusIndexName, new ValueIndexConfiguration("syncStatus"));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database opened at {Directory}")]
    private static partial void LogDatabaseOpened(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listener token removed")]
    private static partial void LogListenerTokenRemoved(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database closed at {Directory}")]
    private static partial void LogDatabaseClosed(ILogger logger, string directory);
}
