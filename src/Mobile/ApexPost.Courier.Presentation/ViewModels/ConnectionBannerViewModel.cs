using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Presentation.Mvvm;
using ApexPost.Courier.Presentation.Ports;
using Polly.CircuitBreaker;

namespace ApexPost.Courier.Presentation.ViewModels;

/// <summary>
/// The global banner "Server unavailable – working offline". Every page shows it while the API circuit breaker is
/// open; one instance (singleton) follows <see cref="ICircuitStateProvider.StateChanged"/> for the whole app.
/// </summary>
public sealed class ConnectionBannerViewModel : ObservableObject, IDisposable
{
    private readonly ICircuitStateProvider _circuit;
    private readonly IUiDispatcher _dispatcher;
    private bool _isOffline;
    private CircuitState _circuitState;

    /// <summary>Creates the banner and reads the current circuit state.</summary>
    /// <param name="circuit">State of the API circuit breaker.</param>
    /// <param name="dispatcher">Moves updates to the UI thread.</param>
    public ConnectionBannerViewModel(ICircuitStateProvider circuit, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _circuit = circuit;
        _dispatcher = dispatcher;
        _isOffline = circuit.IsServerUnavailable;
        _circuitState = circuit.State;
        _circuit.StateChanged += OnStateChanged;
    }

    /// <summary>The banner text.</summary>
    public static string Message => FailureMessages.ServerUnavailable;

    /// <summary>Whether the banner is visible (circuit open or isolated).</summary>
    public bool IsOffline
    {
        get => _isOffline;
        private set => SetProperty(ref _isOffline, value);
    }

    /// <summary>Current circuit state (Closed, Open, HalfOpen, Isolated).</summary>
    public CircuitState CircuitState
    {
        get => _circuitState;
        private set
        {
            if (SetProperty(ref _circuitState, value))
            {
                OnPropertyChanged(nameof(CircuitStateText));
            }
        }
    }

    /// <summary>The circuit state in words, for the Sync Status page.</summary>
    public string CircuitStateText => _circuitState switch
    {
        CircuitState.Closed => "Closed – the server answers normally",
        CircuitState.Open => "Open – calls are paused after repeated failures",
        CircuitState.HalfOpen => "Half-open – testing the server with one call",
        CircuitState.Isolated => "Isolated – calls are blocked",
        _ => _circuitState.ToString(),
    };

    /// <inheritdoc />
    public void Dispose() => _circuit.StateChanged -= OnStateChanged;

    private void OnStateChanged(object? sender, CircuitState state) =>
        _dispatcher.Post(() =>
        {
            CircuitState = state;
            IsOffline = _circuit.IsServerUnavailable;
        });
}
