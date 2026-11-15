using Polly.CircuitBreaker;

namespace ApexPost.Courier.ApiClient.Resilience;

/// <summary>
/// Singleton that the circuit breaker callbacks update and the UI reads through
/// <see cref="ICircuitStateProvider"/>.
/// </summary>
public sealed class CircuitStateTracker : ICircuitStateProvider
{
    private int _state = (int)CircuitState.Closed;

    /// <inheritdoc />
    public event EventHandler<CircuitState>? StateChanged;

    /// <inheritdoc />
    public CircuitState State => (CircuitState)Volatile.Read(ref _state);

    /// <inheritdoc />
    public bool IsServerUnavailable => State is CircuitState.Open or CircuitState.Isolated;

    /// <summary>Records a new state and raises <see cref="StateChanged"/> when it differs from the old one.</summary>
    /// <param name="state">The state the breaker moved to.</param>
    public void Update(CircuitState state)
    {
        var previous = Interlocked.Exchange(ref _state, (int)state);
        if (previous != (int)state)
        {
            StateChanged?.Invoke(this, state);
        }
    }
}
