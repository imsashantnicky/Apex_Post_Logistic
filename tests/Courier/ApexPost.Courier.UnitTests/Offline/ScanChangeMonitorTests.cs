using System.Threading.Channels;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.UnitTests.Offline;

public sealed class ScanChangeMonitorTests : IDisposable
{
    private const string Timestamp = "2026-11-05T08:00:00.0000000+00:00";

    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "apexpost-courier-tests", Guid.NewGuid().ToString("N"));

    private readonly CourierDatabase _database;
    private readonly CouchbaseScanRepository _repository;
    private readonly StringWriter _output = new();
    private readonly Channel<int> _counts = Channel.CreateUnbounded<int>();
    private readonly ScanChangeMonitor _monitor;

    public ScanChangeMonitorTests()
    {
        _database = new CourierDatabase(Path.Combine(_directory, "db"));
        _repository = new CouchbaseScanRepository(_database);
        _monitor = new ScanChangeMonitor(
            _database, _repository, new FixedTimeProvider(Now), NullLogger<ScanChangeMonitor>.Instance,
            _output, LogFilePath);
        _monitor.PendingCountChanged += (_, count) => _counts.Writer.TryWrite(count);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string LogFilePath => Path.Combine(_directory, "logs", "listener.log");

    private async Task<ShipmentScan> AddAsync(SyncStatus status = SyncStatus.Pending)
    {
        var scan = ShipmentScan.Restore(
            ScanIds.New(), "APX-10000001", ScanType.Pickup, "CR-101", "BR001", Now, null, status);
        await _repository.CreateAsync(scan, Ct);
        return scan;
    }

    private async Task<int> NextCountAsync() => await _counts.Reader.ReadAsync(Ct).AsTask().WaitAsync(EventTimeout, Ct);

    [Fact]
    public async Task An_added_scan_is_logged_with_timestamp_kind_id_and_pending_count()
    {
        _monitor.Start();

        var scan = await AddAsync();

        Assert.Equal(1, await NextCountAsync());
        var output = _output.ToString();
        Assert.Contains($"{Timestamp} added {scan.Id}", output, StringComparison.Ordinal);
        Assert.Contains("[LIVE] Pending scans: 1", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_updated_and_a_deleted_scan_are_reported_with_their_kind()
    {
        var scan = await AddAsync();
        _monitor.Start();

        await _repository.UpdateSyncStatusAsync(scan.Id, SyncStatus.Synced, Ct);
        Assert.Equal(0, await NextCountAsync());
        await AddAsync();
        Assert.Equal(1, await NextCountAsync());
        await _repository.DeleteAsync(scan.Id, Ct);
        await _repository.CreateAsync(
            ShipmentScan.Restore(ScanIds.New(), "APX-10000002", ScanType.Pickup, "CR-101", "BR001", Now, null,
                SyncStatus.Pending), Ct);
        Assert.Equal(2, await NextCountAsync());

        var output = _output.ToString();
        Assert.Contains($"updated {scan.Id}", output, StringComparison.Ordinal);
        Assert.Contains($"deleted {scan.Id}", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_lines_are_appended_to_the_log_file()
    {
        _monitor.Start();

        var scan = await AddAsync();
        await NextCountAsync();

        var lines = await File.ReadAllLinesAsync(LogFilePath, Ct);
        Assert.Equal([$"{Timestamp} added {scan.Id}", "[LIVE] Pending scans: 1"], lines);
    }

    [Fact]
    public async Task The_event_is_not_raised_again_when_the_pending_count_stays_the_same()
    {
        _monitor.Start();
        await AddAsync();
        Assert.Equal(1, await NextCountAsync());

        await AddAsync(SyncStatus.Synced);
        await AddAsync();

        Assert.Equal(2, await NextCountAsync());
    }

    [Fact]
    public async Task A_throwing_subscriber_does_not_stop_later_changes_from_being_reported()
    {
        _monitor.PendingCountChanged += (_, _) => throw new InvalidOperationException("Subscriber failed");
        _monitor.Start();

        await AddAsync();
        Assert.Equal(1, await NextCountAsync());
        await AddAsync();

        Assert.Equal(2, await NextCountAsync());
    }

    [Fact]
    public async Task Calling_Start_twice_registers_one_listener()
    {
        _monitor.Start();
        _monitor.Start();

        await AddAsync();
        await NextCountAsync();

        var lines = await File.ReadAllLinesAsync(LogFilePath, Ct);
        Assert.Single(lines, line => line.Contains("added", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _database.Dispose();
        _output.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
