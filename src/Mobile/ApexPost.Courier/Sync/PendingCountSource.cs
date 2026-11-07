using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Sync;

/// <summary>Forwards the live pending count of the Part A change listener to the view models.</summary>
/// <param name="monitor">The Couchbase change listener.</param>
public sealed class PendingCountSource(ScanChangeMonitor monitor) : IPendingCountSource
{
    /// <inheritdoc />
    public event EventHandler<int>? PendingCountChanged
    {
        add => monitor.PendingCountChanged += value;
        remove => monitor.PendingCountChanged -= value;
    }
}
