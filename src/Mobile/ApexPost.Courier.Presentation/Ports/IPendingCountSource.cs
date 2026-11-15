namespace ApexPost.Courier.Presentation.Ports;

/// <summary>
/// Reports the live number of pending scans. In the app it forwards the Part A <c>ScanChangeMonitor</c> event.
/// </summary>
public interface IPendingCountSource
{
    /// <summary>Raised with the new pending count after a change, on a background thread.</summary>
    event EventHandler<int>? PendingCountChanged;
}
