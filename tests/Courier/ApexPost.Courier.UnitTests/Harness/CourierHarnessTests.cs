using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Harness;
using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.UnitTests.Harness;

public sealed class CourierHarnessTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "apexpost-courier-tests", Guid.NewGuid().ToString("N"));

    private readonly CourierDatabase _database;
    private readonly CouchbaseScanRepository _repository;
    private readonly OutboxService _outbox;
    private readonly StringWriter _output = new();

    public CourierHarnessTests()
    {
        _database = new CourierDatabase(_directory);
        _repository = new CouchbaseScanRepository(_database);
        _outbox = new OutboxService(
            _repository, _repository, new FakeShipmentUploader(seed: 7), NullLogger<OutboxService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Output => _output.ToString();

    private Task RunAsync(string script, CancellationToken? ct = null) =>
        new CourierHarness(_database, _repository, _outbox, new FixedTimeProvider(Now), new StringReader(script),
            _output).RunAsync(ct ?? Ct);

    private static string Lines(params string[] lines) => string.Join('\n', lines) + "\n";

    [Fact]
    public async Task Header_shows_database_name_path_pending_count_and_every_menu_label()
    {
        await RunAsync(Lines("6"));

        Assert.Contains($"Database : {CourierDatabase.DatabaseName}", Output, StringComparison.Ordinal);
        Assert.Contains($"Path     : {_directory}", Output, StringComparison.Ordinal);
        Assert.Contains("Pending  : 0 scan(s)", Output, StringComparison.Ordinal);
        string[] labels =
        [
            "1  Add Scan", "2  Find by Tracking Number", "3  List Pending", "4  Simulate Sync", "5  Delete Scan",
            "6  Exit", "7  Scans per Type for Courier", "8  Update Scan Notes", "9  Get Scan by ID",
            "10  Seed Fictional Demo Data", "11  Show Query Plans",
        ];
        Assert.All(labels, label => Assert.Contains(label, Output, StringComparison.Ordinal));
        Assert.Contains("Exiting.", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seeding_twice_adds_the_demo_scans_once_and_all_start_pending()
    {
        await RunAsync(Lines("10", "10", "6"));

        var scans = await _repository.ListAllAsync(Ct);
        Assert.Equal(8, scans.Count);
        Assert.All(scans, scan => Assert.Equal(SyncStatus.Pending, scan.SyncStatus));
        Assert.Equal(["CR-101", "CR-102"], scans.Select(scan => scan.CourierId).Distinct().Order());
        Assert.Contains(scans, scan => scan.TrackingNumber == FakeShipmentUploader.DemoFailureTrackingNumber);
        Assert.Contains("Demo data is already present", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Simulate_sync_after_seeding_syncs_seven_scans_and_fails_the_demo_failure()
    {
        await RunAsync(Lines("10", "4", "n", "6"));

        Assert.Contains("8 attempted, 7 synced, 1 failed", Output, StringComparison.Ordinal);
        var failed = Assert.Single(await _repository.GetFailedAsync(Ct));
        Assert.Equal(FakeShipmentUploader.DemoFailureTrackingNumber, failed.TrackingNumber);
        Assert.Equal(0, await _repository.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task Invalid_input_is_rejected_and_asked_again_without_crashing()
    {
        await RunAsync(Lines(
            "abc", "1", "APX-1", "apx-10000007", "9", "pickup", "CR-1", "cr-102", "Maple Depot", "", "6"));

        Assert.Contains("Unknown option 'abc'", Output, StringComparison.Ordinal);
        Assert.Contains("Invalid value 'APX-1'", Output, StringComparison.Ordinal);
        Assert.Contains("Invalid value '9'", Output, StringComparison.Ordinal);
        Assert.Contains("Invalid value 'CR-1'", Output, StringComparison.Ordinal);
        var scan = Assert.Single(await _repository.ListAllAsync(Ct));
        Assert.Equal(("APX-10000007", ScanType.Pickup, "CR-102"), (scan.TrackingNumber, scan.ScanType, scan.CourierId));
        Assert.Equal(SyncStatus.Pending, scan.SyncStatus);
    }

    [Fact]
    public async Task Update_notes_saves_the_text_and_puts_a_synced_scan_back_to_pending()
    {
        var scan = await AddAsync(SyncStatus.Synced);

        await RunAsync(Lines("8", scan.Id, "Parcel left at gate", "6"));

        var stored = await _repository.GetByIdAsync(scan.Id, Ct);
        Assert.Equal("Parcel left at gate", stored?.Notes);
        Assert.Equal(SyncStatus.Pending, stored?.SyncStatus);
    }

    [Fact]
    public async Task Delete_removes_the_scan_only_after_confirmation()
    {
        var scan = await AddAsync(SyncStatus.Pending);

        await RunAsync(Lines("5", scan.Id, "n", "5", scan.Id, "y", "6"));

        Assert.Contains("Cancelled.", Output, StringComparison.Ordinal);
        Assert.Contains($"Deleted {scan.Id}.", Output, StringComparison.Ordinal);
        Assert.Null(await _repository.GetByIdAsync(scan.Id, Ct));
    }

    [Fact]
    public async Task Scans_per_type_lists_every_type_including_zero_counts()
    {
        await RunAsync(Lines("10", "7", "CR-101", "6"));

        Assert.Contains("Pickup    | 2", Output, StringComparison.Ordinal);
        Assert.Contains("InTransit | 1", Output, StringComparison.Ordinal);
        Assert.Contains("Failed    | 0", Output, StringComparison.Ordinal);
        Assert.Contains("Total: 4", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Query_plans_show_that_both_value_indexes_are_used()
    {
        await RunAsync(Lines("11", "6"));

        Assert.Contains($"uses {CourierDatabase.TrackingNumberIndexName} -> yes", Output, StringComparison.Ordinal);
        Assert.Contains($"uses {CourierDatabase.SyncStatusIndexName} -> yes", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task End_of_input_and_cancellation_both_end_the_menu_cleanly()
    {
        await RunAsync(string.Empty);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await RunAsync(Lines("3"), cancelled.Token);

        Assert.Contains("Exiting.", Output, StringComparison.Ordinal);
        Assert.Contains("Ctrl+C received.", Output, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _outbox.Dispose();
        _database.Dispose();
        _output.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task<ShipmentScan> AddAsync(SyncStatus status)
    {
        var scan = ShipmentScan.Restore(
            ScanIds.New(), "APX-10000001", ScanType.Pickup, "CR-101", "Maple Depot", Now, null, status);
        await _repository.CreateAsync(scan, Ct);
        return scan;
    }
}
