using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.Sync;

namespace ApexPost.Courier;

/// <summary>
/// The courier application. It opens the main page when a token is stored and the sign-in page otherwise, starts
/// the Part A change listener and the sync triggers, and sends the courier to sign-in after a 401.
/// </summary>
public partial class App : Application
{
    private const string SessionExpired = "Your session has expired. Sign in again; your scans are kept.";

    private readonly IAccessTokenStore _tokens;
    private readonly AppNavigator _navigator;
    private readonly CourierSync _sync;
    private readonly ScanChangeMonitor _monitor;

    /// <summary>Creates the app with its services from dependency injection.</summary>
    /// <param name="tokens">Secure token store.</param>
    /// <param name="navigator">Switches between sign-in and main page.</param>
    /// <param name="sync">Sync triggers.</param>
    /// <param name="monitor">Part A change listener.</param>
    public App(IAccessTokenStore tokens, AppNavigator navigator, CourierSync sync, ScanChangeMonitor monitor)
    {
        InitializeComponent();
        _tokens = tokens;
        _navigator = navigator;
        _sync = sync;
        _monitor = monitor;
        _sync.SignInRequired += (_, _) => _ = _navigator.ShowLoginAsync(SessionExpired);
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new ContentPage()) { Title = "ApexPost Courier" };

    /// <inheritdoc />
    protected override async void OnStart()
    {
        base.OnStart();
        _monitor.Start();
        _sync.StartListening();

        var signedIn = !string.IsNullOrWhiteSpace(await _tokens.GetAccessTokenAsync(CancellationToken.None));
        if (signedIn)
        {
            await _navigator.ShowMainAsync();
            _sync.SyncInBackground("start-up");
        }
        else
        {
            await _navigator.ShowLoginAsync();
        }
    }
}
