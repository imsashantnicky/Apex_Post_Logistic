using System.Globalization;
using ApexPost.Courier.Domain.Ports;
using Couchbase.Lite;
using Couchbase.Lite.Query;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Offline.Scans;

/// <summary>
/// Watches the scan collection and reports every change live: the time, each changed document id with whether it
/// was added, updated or deleted, and then the number of scans still waiting to be uploaded.
/// </summary>
/// <remarks>
/// Couchbase Lite calls the listener on a background thread, so writes are serialised with a lock and every
/// exception inside the callback is caught and logged; a failing listener never affects the database.
/// The listener token is handed to <see cref="CourierDatabase"/>, which removes it before closing.
/// </remarks>
public sealed partial class ScanChangeMonitor
{
    /// <summary>Default log file, relative to the current directory.</summary>
    public static readonly string DefaultLogFilePath = Path.Combine("logs", "listener.log");

    private const string ScanIdsQuery = "SELECT META().id AS id FROM _ WHERE type = $type";
    private const string TypeParameter = "type";
    private const string IdColumn = "id";
    private const string PendingLinePrefix = "[LIVE] Pending scans: ";

    private readonly CourierDatabase _database;
    private readonly IScanQueries _queries;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScanChangeMonitor> _logger;
    private readonly TextWriter _output;
    private readonly string _logFilePath;
    private readonly Lock _gate = new();
    private readonly HashSet<string> _knownIds = new(StringComparer.Ordinal);
    private int? _lastPendingCount;
    private bool _started;

    /// <summary>Creates a monitor over an open database. Call <see cref="Start"/> to begin listening.</summary>
    /// <param name="database">The database to watch; it owns and later removes the listener token.</param>
    /// <param name="queries">Counts the pending scans after each change.</param>
    /// <param name="timeProvider">Supplies the timestamp of each change line.</param>
    /// <param name="logger">Logs exceptions raised inside the callback.</param>
    /// <param name="output">Where live lines are written; defaults to the console.</param>
    /// <param name="logFilePath">
    /// File the same lines are appended to; defaults to <see cref="DefaultLogFilePath"/>.
    /// </param>
    public ScanChangeMonitor(
        CourierDatabase database,
        IScanQueries queries,
        TimeProvider timeProvider,
        ILogger<ScanChangeMonitor> logger,
        TextWriter? output = null,
        string? logFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _database = database;
        _queries = queries;
        _timeProvider = timeProvider;
        _logger = logger;
        _output = output ?? Console.Out;
        _logFilePath = string.IsNullOrWhiteSpace(logFilePath) ? DefaultLogFilePath : logFilePath;
    }

    /// <summary>
    /// Raised with the new number of pending scans after the first change and whenever the number changes.
    /// Raised on a background thread; a UI must switch to its own thread before touching controls.
    /// </summary>
    public event EventHandler<int>? PendingCountChanged;

    /// <summary>Starts listening for changes. Calling it again does nothing.</summary>
    /// <exception cref="ObjectDisposedException">The database was disposed.</exception>
    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            LoadKnownIds();
            var collection = _database.GetDefaultCollection();
            _database.RegisterListenerToken(collection.AddChangeListener(OnCollectionChanged));
        }
    }

    // Couchbase Lite needs a synchronous handler. CountPendingAsync completes synchronously, and every exception
    // is caught inside HandleChangeAsync, so the discarded task can never fault unobserved.
    private void OnCollectionChanged(object? sender, CollectionChangedEventArgs args) =>
        _ = HandleChangeAsync(args.Collection, args.DocumentIDs);

    private async Task HandleChangeAsync(Collection collection, IReadOnlyList<string> documentIds)
    {
        try
        {
            var changeLines = DescribeChanges(collection, documentIds);
            var pending = await _queries.CountPendingAsync(CancellationToken.None).ConfigureAwait(false);
            bool changed;
            lock (_gate)
            {
                Write([.. changeLines, PendingLinePrefix + pending]);
                changed = pending != _lastPendingCount;
                _lastPendingCount = pending;
            }

            if (changed)
            {
                PendingCountChanged?.Invoke(this, pending);
            }
        }
        catch (Exception ex)
        {
            LogListenerFailed(_logger, ex);
        }
    }

    private List<string> DescribeChanges(Collection collection, IReadOnlyList<string> documentIds)
    {
        var timestamp = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        var lines = new List<string>(documentIds.Count);
        lock (_gate)
        {
            foreach (var id in documentIds)
            {
                lines.Add($"{timestamp} {Classify(collection, id)} {id}");
            }
        }

        return lines;
    }

    // Change events carry only ids, so the kind is worked out from the ids seen before and the current state.
    private string Classify(Collection collection, string id)
    {
        if (collection.GetDocument(id) is null)
        {
            _knownIds.Remove(id);
            return "deleted";
        }

        return _knownIds.Add(id) ? "added" : "updated";
    }

    private void LoadKnownIds()
    {
        using var query = _database.CreateQuery(ScanIdsQuery);
        query.Parameters = new Parameters().SetString(TypeParameter, ScanDocumentMapper.TypeValue);
        using var results = query.Execute();
        foreach (var row in results)
        {
            if (row.GetString(IdColumn) is { } id)
            {
                _knownIds.Add(id);
            }
        }
    }

    // Called inside the lock, so lines from two callbacks never interleave on the console or in the file.
    private void Write(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            _output.WriteLine(line);
        }

        var folder = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.AppendAllLines(_logFilePath, lines);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The scan change listener failed")]
    private static partial void LogListenerFailed(ILogger logger, Exception exception);
}
