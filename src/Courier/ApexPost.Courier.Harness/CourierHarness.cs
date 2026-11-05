using System.Globalization;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Scans;

namespace ApexPost.Courier.Harness;

/// <summary>
/// The console menu over the offline scan store. Every action catches its own errors and prints them, so a bad
/// input or a storage error returns to the menu instead of ending the program.
/// </summary>
internal sealed class CourierHarness
{
    private const string ExitKey = "6";
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
    private const int LocationWidth = 16;
    private const int NotesWidth = 24;
    private const string Banner = "==============================================================================";

    private static readonly (string Key, string Label)[] MenuItems =
    [
        ("1", "Add Scan"),
        ("2", "Find by Tracking Number"),
        ("3", "List Pending"),
        ("4", "Simulate Sync"),
        ("5", "Delete Scan"),
        (ExitKey, "Exit"),
        ("7", "Scans per Type for Courier"),
        ("8", "Update Scan Notes"),
        ("9", "Get Scan by ID"),
        ("10", "Seed Fictional Demo Data"),
        ("11", "Show Query Plans"),
    ];

    private static readonly string[] ScanHeaders =
        ["Id", "Tracking", "Type", "Courier", "Location", "Scanned (UTC)", "Sync", "Notes"];

    private readonly CourierDatabase _database;
    private readonly CouchbaseScanRepository _repository;
    private readonly OutboxService _outbox;
    private readonly TimeProvider _timeProvider;
    private readonly ConsoleInput _input;
    private readonly TextWriter _output;

    /// <summary>Creates the menu over an open database. The caller owns and disposes every dependency.</summary>
    public CourierHarness(
        CourierDatabase database,
        CouchbaseScanRepository repository,
        OutboxService outbox,
        TimeProvider timeProvider,
        TextReader input,
        TextWriter output)
    {
        _database = database;
        _repository = repository;
        _outbox = outbox;
        _timeProvider = timeProvider;
        _input = new ConsoleInput(input, output);
        _output = output;
    }

    /// <summary>Shows the menu until Exit is chosen, the input ends or <paramref name="ct"/> is cancelled.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await WriteHeaderAsync(ct);
                var choice = _input.ReadLine("Choose an option");
                if (choice is null || ct.IsCancellationRequested || choice == ExitKey)
                {
                    break;
                }

                await RunActionAsync(choice, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Ctrl+C during an action: stop here; the caller disposes the database.
        }

        _output.WriteLine();
        _output.WriteLine(ct.IsCancellationRequested ? "Ctrl+C received. Closing..." : "Exiting. Closing...");
    }

    private async Task WriteHeaderAsync(CancellationToken ct)
    {
        var pending = await _repository.CountPendingAsync(ct);
        _output.WriteLine();
        _output.WriteLine(Banner);
        _output.WriteLine(" ApexPost Courier Harness - offline scan store (Couchbase Lite)");
        _output.WriteLine($" Database : {CourierDatabase.DatabaseName}");
        _output.WriteLine($" Path     : {_database.Directory}");
        _output.WriteLine($" Pending  : {pending} scan(s) waiting to upload");
        _output.WriteLine(Banner);
        foreach (var (key, label) in MenuItems)
        {
            _output.WriteLine($" {key,2}  {label}");
        }

        _output.WriteLine();
    }

    private async Task RunActionAsync(string choice, CancellationToken ct)
    {
        try
        {
            await (choice switch
            {
                "1" => AddScanAsync(ct),
                "2" => FindByTrackingNumberAsync(ct),
                "3" => ListPendingAsync(ct),
                "4" => SimulateSyncAsync(ct),
                "5" => DeleteScanAsync(ct),
                "7" => ScansPerTypeAsync(ct),
                "8" => UpdateNotesAsync(ct),
                "9" => GetByIdAsync(ct),
                "10" => SeedAsync(ct),
                "11" => ShowQueryPlans(),
                _ => UnknownOption(choice),
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // The harness must never crash: report the problem and return to the menu.
            _output.WriteLine($"  Error ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private Task UnknownOption(string choice)
    {
        _output.WriteLine($"  Unknown option '{choice}'. Enter a number from 1 to {MenuItems.Length}.");
        return Task.CompletedTask;
    }

    private async Task AddScanAsync(CancellationToken ct)
    {
        if (ReadTrackingNumber() is not { } tracking
            || _input.ReadScanType() is not { } scanType
            || ReadCourierId() is not { } courierId
            || _input.ReadRequired("Location") is not { } location
            || _input.ReadOptional("Notes (optional)") is not { } notes)
        {
            Cancelled();
            return;
        }

        var scan = ShipmentScan.CreateNew(tracking, scanType, courierId, location, notes, _timeProvider);
        await _repository.CreateAsync(scan, ct);
        _output.WriteLine($"  Saved locally as {scan.SyncStatus}: {scan.Id}");
        WriteScans([scan]);
    }

    private async Task FindByTrackingNumberAsync(CancellationToken ct)
    {
        if (ReadTrackingNumber() is not { } tracking)
        {
            Cancelled();
            return;
        }

        var scans = await _repository.GetByTrackingNumberAsync(tracking, ct);
        WriteScans(scans, $"No scans for {tracking}.");
    }

    private async Task ListPendingAsync(CancellationToken ct)
    {
        var scans = await _repository.GetPendingAsync(ct);
        WriteScans(scans, "No pending scans. Everything has been uploaded.");
    }

    private async Task SimulateSyncAsync(CancellationToken ct)
    {
        var retryFailed = _input.ReadYesNo("Also retry scans that failed before?");
        _output.WriteLine(
            $"  Uploading through {nameof(FakeShipmentUploader)} ({FakeShipmentUploader.DemoFailureTrackingNumber} " +
            "is always rejected)...");
        var result = await _outbox.ProcessPendingAsync(retryFailed, ct);
        _output.WriteLine(
            $"  Sync finished: {result.Attempted} attempted, {result.Synced} synced, {result.Failed} failed.");

        var failed = await _repository.GetFailedAsync(ct);
        if (failed.Count > 0)
        {
            _output.WriteLine("  Scans with a failed upload (retry with option 4 and answer y):");
            WriteScans(failed);
        }
    }

    private async Task DeleteScanAsync(CancellationToken ct)
    {
        if (await ReadExistingScanAsync(ct) is not { } scan)
        {
            return;
        }

        if (!_input.ReadYesNo($"Delete scan {scan.Id}?"))
        {
            Cancelled();
            return;
        }

        var deleted = await _repository.DeleteAsync(scan.Id, ct);
        _output.WriteLine(deleted ? $"  Deleted {scan.Id}." : $"  Scan {scan.Id} was already gone.");
    }

    private async Task ScansPerTypeAsync(CancellationToken ct)
    {
        if (ReadCourierId() is not { } courierId)
        {
            Cancelled();
            return;
        }

        var counts = (await _repository.CountByScanTypeAsync(courierId, ct))
            .ToDictionary(count => count.ScanType, count => count.Count);
        var rows = Enum.GetValues<ScanType>()
            .Select(type => (IReadOnlyList<string>)[type.ToString(), Number(counts.GetValueOrDefault(type))]);

        _output.WriteLine($"  Scans per type for {courierId}:");
        TableWriter.Write(_output, ["Scan type", "Count"], rows);
        _output.WriteLine($"  Total: {counts.Values.Sum()}");
    }

    private async Task UpdateNotesAsync(CancellationToken ct)
    {
        if (await ReadExistingScanAsync(ct) is not { } scan
            || _input.ReadOptional("New notes (leave empty to clear)") is not { } notes)
        {
            return;
        }

        var edited = ShipmentScan.Restore(
            scan.Id, scan.TrackingNumber, scan.ScanType, scan.CourierId, scan.Location, scan.ScannedAtUtc,
            notes, scan.SyncStatus);
        await _repository.UpdateAsync(edited, ct);
        _output.WriteLine("  Notes saved. The scan is Pending again so the change is uploaded on the next sync.");
        WriteScans([await _repository.GetByIdAsync(scan.Id, ct) ?? edited]);
    }

    private async Task GetByIdAsync(CancellationToken ct)
    {
        if (await ReadExistingScanAsync(ct) is { } scan)
        {
            _output.WriteLine("  Found by key-value lookup on the document id (no query).");
            WriteScans([scan]);
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        var added = await DemoData.SeedAsync(_repository, _repository, _timeProvider, ct);
        _output.WriteLine(added == 0
            ? $"  Demo data is already present ({DemoData.FirstTrackingNumber} has scans). Nothing added."
            : $"  Added {added} fictional Pending scans.");
    }

    private Task ShowQueryPlans()
    {
        _output.WriteLine($"  Indexes: {string.Join(", ", _database.GetIndexNames())}");
        var expectedIndex = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(CouchbaseScanRepository.GetByTrackingNumberAsync)] = CourierDatabase.TrackingNumberIndexName,
            [nameof(CouchbaseScanRepository.GetPendingAsync)] = CourierDatabase.SyncStatusIndexName,
        };

        foreach (var (method, plan) in _repository.ExplainQueries())
        {
            var index = expectedIndex.GetValueOrDefault(method, string.Empty);
            var used = index.Length > 0 && plan.Contains($"USING INDEX {index}", StringComparison.Ordinal);
            _output.WriteLine();
            _output.WriteLine($"  {method}: uses {index} -> {(used ? "yes" : "NO")}");
            _output.WriteLine(plan.TrimEnd());
        }

        return Task.CompletedTask;
    }

    private async Task<ShipmentScan?> ReadExistingScanAsync(CancellationToken ct)
    {
        var id = _input.ReadValid("Scan id", ScanIds.IsValid, $"a scan id looks like {ScanIds.Prefix}{{guid}}.");
        if (id is null)
        {
            Cancelled();
            return null;
        }

        var scan = await _repository.GetByIdAsync(id, ct);
        if (scan is null)
        {
            _output.WriteLine($"  No scan with id {id}.");
        }

        return scan;
    }

    private string? ReadTrackingNumber() => _input.ReadValid(
        "Tracking number (APX-########)", ScanRules.IsValidTrackingNumber,
        $"must match {ScanRules.TrackingNumberPattern}.", text => text.ToUpperInvariant());

    private string? ReadCourierId() => _input.ReadValid(
        "Courier id (CR-###)", ScanRules.IsValidCourierId,
        $"must match {ScanRules.CourierIdPattern}.", text => text.ToUpperInvariant());

    private void Cancelled() => _output.WriteLine("  Cancelled. Back to the menu.");

    private void WriteScans(IReadOnlyList<ShipmentScan> scans, string emptyMessage = "No scans.")
    {
        if (scans.Count == 0)
        {
            _output.WriteLine($"  {emptyMessage}");
            return;
        }

        TableWriter.Write(_output, ScanHeaders, scans.Select(ScanRow));
        _output.WriteLine($"  {scans.Count} scan(s).");
    }

    private static IReadOnlyList<string> ScanRow(ShipmentScan scan) =>
    [
        scan.Id,
        scan.TrackingNumber,
        scan.ScanType.ToString(),
        scan.CourierId,
        TableWriter.Truncate(scan.Location, LocationWidth),
        scan.ScannedAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture),
        scan.SyncStatus.ToString(),
        TableWriter.Truncate(scan.Notes, NotesWidth),
    ];

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
