using Polly.CircuitBreaker;

namespace ApexPost.Courier.ApiClient.Resilience;

/// <summary>
/// Read-only view of the API circuit breaker for the UI. While the circuit is open the app shows
/// "Server unavailable – working offline" and keeps saving scans locally.
/// </summary>
public interface ICircuitStateProvider
{
    /// <summary>Current state of the circuit.</summary>
    CircuitState State { get; }

    /// <summary>True while calls are blocked (open or isolated circuit).</summary>
    bool IsServerUnavailable { get; }

    /// <summary>Raised after every state change, on the thread of the call that caused it.</summary>
    event EventHandler<CircuitState>? StateChanged;
}
