using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Services;

/// <summary>Runs view model updates on the MAUI main thread.</summary>
public sealed class MainThreadDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
