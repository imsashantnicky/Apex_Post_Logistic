using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.Presentation.Ports;
using ApexPost.Courier.Presentation.ViewModels;
using ApexPost.Courier.Sync;

namespace ApexPost.Courier;

/// <summary>
/// The courier application. It opens the Shell on the sign-in route, switches to the main tabs when a token is
/// stored, starts the Part A change listener and the sync triggers, and sends the courier to sign-in after a 401.
/// </summary>
public partial class App : Application
{
    private const string SessionExpired = "Your session has expired. Sign in again; your scans are kept.";

    private readonly AppShell _shell;
    private readonly IAccessTokenStore _tokens;
    private readonly IAppNavigator _navigator;
    private readonly CourierSync _sync;
    private readonly ScanChangeMonitor _monitor;

    /// <summary>Creates the app with its services from dependency injection.</summary>
    /// <param name="shell">The Shell with the sign-in route and the main tabs.</param>
    /// <param name="tokens">Secure token store.</param>
    /// <param name="navigator">Switches between sign-in and the main tabs.</param>
    /// <param name="sync">Sync triggers.</param>
    /// <param name="monitor">Part A change listener.</param>
    /// <param name="login">Sign-in view model, to show "session expired".</param>
    /// <param name="dispatcher">Moves the 401 reaction to the UI thread.</param>
    public App(
        AppShell shell,
        IAccessTokenStore tokens,
        IAppNavigator navigator,
        CourierSync sync,
        ScanChangeMonitor monitor,
        LoginViewModel login,
        IUiDispatcher dispatcher)
    {
        InitializeComponent();
        (_shell, _tokens, _navigator, _sync, _monitor) = (shell, tokens, navigator, sync, monitor);
        _sync.SignInRequired += (_, _) => dispatcher.Post(() =>
        {
            login.ShowError(SessionExpired);
            _ = _navigator.ShowLoginAsync();
        });
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_shell) { Title = "ApexPost Courier" };

    /// <inheritdoc />
    protected override async void OnStart()
    {
        base.OnStart();
        _monitor.Start();
        _sync.StartListening();

        if (!string.IsNullOrWhiteSpace(await _tokens.GetAccessTokenAsync(CancellationToken.None)))
        {
            await _navigator.ShowMainAsync();
            _sync.SyncInBackground("start-up");
        }
    }
}
