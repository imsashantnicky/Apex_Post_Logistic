namespace ApexPost.Courier.Presentation.Ports;

/// <summary>
/// Runs an action on the UI thread. Events from the Couchbase listener, the circuit breaker and the outbox arrive
/// on background threads; view models pass their updates through this port. Tests run the action at once.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Queues <paramref name="action"/> on the UI thread (or runs it now when already there).</summary>
    /// <param name="action">The update to run.</param>
    void Post(Action action);
}
