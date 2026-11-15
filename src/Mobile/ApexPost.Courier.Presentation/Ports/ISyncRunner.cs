using ApexPost.Courier.Offline.Outbox;

namespace ApexPost.Courier.Presentation.Ports;

/// <summary>Starts outbox runs for the screens. The app's <c>CourierSync</c> implements it.</summary>
public interface ISyncRunner
{
    /// <summary>Raised after every run that uploaded or deferred something, on a background thread.</summary>
    event EventHandler<OutboxResult>? SyncCompleted;

    /// <summary>Runs the outbox once in the background; never throws.</summary>
    /// <param name="trigger">Why the run starts, for the log.</param>
    void SyncInBackground(string trigger);

    /// <summary>Runs the outbox once and returns its counts.</summary>
    /// <param name="trigger">Why the run starts, for the log.</param>
    /// <param name="ct">Stops the run between batches.</param>
    Task<OutboxResult> SyncNowAsync(string trigger, CancellationToken ct);
}
