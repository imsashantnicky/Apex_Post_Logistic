using System.Globalization;
using ApexPost.Courier.ApiClient;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.Domain.Ports;
using Polly.CircuitBreaker;

namespace ApexPost.Courier;

/// <summary>
/// Start page: stores the courier token in SecureStorage, looks up a shipment through the typed client and shows
/// the number of scans still waiting on the device. Every API failure is shown as its friendly message, and a
/// banner says "Server unavailable – working offline" while the API circuit breaker is open.
/// </summary>
public partial class MainPage : ContentPage
{
    private static readonly Color ErrorColor = Color.FromArgb("#B3261E");
    private static readonly Color OkColor = Color.FromArgb("#1B7F4B");

    private readonly ApexApiClient _api;
    private readonly IAccessTokenStore _tokens;
    private readonly IScanQueries _scans;
    private readonly ICircuitStateProvider _circuit;

    /// <summary>Creates the page with its services.</summary>
    /// <param name="api">Typed API client.</param>
    /// <param name="tokens">Secure token store.</param>
    /// <param name="scans">Local scan queries.</param>
    /// <param name="circuit">State of the API circuit breaker, for the offline banner.</param>
    public MainPage(ApexApiClient api, IAccessTokenStore tokens, IScanQueries scans, ICircuitStateProvider circuit)
    {
        InitializeComponent();
        _api = api;
        _tokens = tokens;
        _scans = scans;
        _circuit = circuit;
        ApiAddressLabel.Text = $"API: {api.BaseAddress}";
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _circuit.StateChanged += OnCircuitStateChanged;
        OfflineBanner.IsVisible = _circuit.IsServerUnavailable;
        var pending = await _scans.GetPendingAsync(CancellationToken.None);
        PendingLabel.Text = string.Create(CultureInfo.CurrentCulture, $"Pending scans on this device: {pending.Count}");
        var token = await _tokens.GetAccessTokenAsync(CancellationToken.None);
        ShowTokenStatus(string.IsNullOrEmpty(token) ? "Not signed in." : "Token stored securely.", OkColor);
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        _circuit.StateChanged -= OnCircuitStateChanged;
        base.OnDisappearing();
    }

    // Raised on the thread of the HTTP call that changed the state, so the banner is updated on the UI thread.
    private void OnCircuitStateChanged(object? sender, CircuitState state) =>
        Dispatcher.Dispatch(() => OfflineBanner.IsVisible = _circuit.IsServerUnavailable);

    private async void OnSaveTokenClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TokenEntry.Text))
        {
            ShowTokenStatus("Paste a token first.", ErrorColor);
            return;
        }

        await _tokens.SaveAccessTokenAsync(TokenEntry.Text, CancellationToken.None);
        TokenEntry.Text = string.Empty;
        ShowTokenStatus("Token stored securely.", OkColor);
    }

    private void OnSignOutClicked(object? sender, EventArgs e)
    {
        _tokens.Clear();
        ShowTokenStatus("Signed out.", OkColor);
    }

    private async void OnLookupClicked(object? sender, EventArgs e)
    {
        SetBusy(true);
        var trackingNumber = (TrackingEntry.Text ?? string.Empty).Trim().ToUpperInvariant();
        var result = await _api.GetShipmentAsync(trackingNumber, CancellationToken.None);
        SetBusy(false);

        if (result.IsSuccess)
        {
            ResultLabel.TextColor = OkColor;
            ResultLabel.Text = Describe(result.Value);
        }
        else
        {
            ResultLabel.TextColor = ErrorColor;
            ResultLabel.Text = result.Failure.TraceId is null
                ? result.Failure.Message
                : $"{result.Failure.Message}\nReference: {result.Failure.TraceId}";
        }
    }

    private static string Describe(ShipmentResponse shipment)
    {
        var latest = shipment.LatestScan is { } scan
            ? string.Create(CultureInfo.CurrentCulture, $"{scan.ScanType} at {scan.Location}, {scan.ScannedAtUtc.ToLocalTime():g}")
            : "No scans yet";
        return $"{shipment.TrackingNumber}: {shipment.Status}\nTo {shipment.Recipient.City}\nLatest: {latest}";
    }

    private void SetBusy(bool busy)
    {
        Busy.IsVisible = busy;
        Busy.IsRunning = busy;
        LookupButton.IsEnabled = !busy;
    }

    private void ShowTokenStatus(string text, Color color)
    {
        TokenStatusLabel.Text = text;
        TokenStatusLabel.TextColor = color;
    }
}
