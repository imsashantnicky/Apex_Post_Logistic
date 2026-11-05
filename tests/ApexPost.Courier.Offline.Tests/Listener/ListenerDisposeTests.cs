using System.Threading.Channels;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.Offline.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.Offline.Tests.Listener;

public sealed class ListenerDisposeTests : IDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    // Long enough for Couchbase Lite to deliver a change notification if a listener were still attached.
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(1);

    private readonly TempCourierDatabase _db = new();
    private readonly StringWriter _output = new();
    private readonly Channel<int> _counts = Channel.CreateUnbounded<int>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_listener_callback_arrives_after_the_database_is_disposed()
    {
        var monitor = new ScanChangeMonitor(
            _db.Database, _db.Repository, new FixedTimeProvider(TestScans.Start),
            NullLogger<ScanChangeMonitor>.Instance, _output, Path.Combine(_db.Folder, "logs", "listener.log"));
        monitor.PendingCountChanged += (_, count) => _counts.Writer.TryWrite(count);
        monitor.Start();

        // Control: while the database is open, a change reaches the listener.
        var before = TestScans.At(0);
        await _db.Repository.CreateAsync(before, Ct);
        Assert.Equal(1, await _counts.Reader.ReadAsync(Ct).AsTask().WaitAsync(EventTimeout, Ct));

        _db.Database.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _db.Repository.CreateAsync(TestScans.At(1), Ct));

        // A write through a new instance of the same database must not reach the removed listener.
        using (var reopened = new CourierDatabase(_db.DatabaseFolder))
        {
            await new CouchbaseScanRepository(reopened).CreateAsync(TestScans.At(2), Ct);
            await Task.Delay(QuietPeriod, Ct);
        }

        Assert.False(_counts.Reader.TryRead(out _));
        Assert.Equal(1, _output.ToString().Split(" added ").Length - 1);
    }

    public void Dispose()
    {
        _db.Dispose();
        _output.Dispose();
    }
}
