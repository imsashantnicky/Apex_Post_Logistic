using System.Globalization;
using ApexPost.Courier.ApiClient;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.Sync;
using Polly.CircuitBreaker;

namespace ApexPost.Courier;

/// <summary>
/// Main page: records scans on the device (always Pending first), shows the live number of pending scans from the
/// Part A change listener, starts a sync on request, and looks up a shipment. A banner says "Server unavailable –
/// working offline" while the API circuit breaker is open. Events from background threads update controls through
/// the page's dispatcher.
/// </summary>
public partial class MainPage : ContentPage
{
    private static readonly Color ErrorColor = Color.FromArgb("#B3261E");
    private static readonly Color OkColor = Color.FromArgb("#1B7F4B");

    // Delivered is left out until the device stores the recipient signature the API requires for it.
    private static readonly ScanType[] RecordableScanTypes = [ScanType.Pickup, ScanType.InTransit, ScanType.Failed];

    private readonly ApexApiClient _api;
    private readonly IAccessTokenStore _tokens;
    private readonly IScanQueries _queries;
    private readonly IScanRepository _scans;
    private readonly ScanChangeMonitor _monitor;
    private readonly CourierSync _sync;
    private readonly ICircuitStateProvider _circuit;
    private readonly AppNavigator _navigator;

    /// <summary>Creates the page with its services.</summary>
    /// <param name="api">Typed API client.</param>
    /// <param name="tokens">Secure token store.</param>
    /// <param name="queries">Local scan queries.</param>
    /// <param name="scans">Local scan store.</param>
    /// <param name="monitor">Part A change listener; raises the pending count.</param>
    /// <param name="sync">Starts outbox runs.</param>
    /// <param name="circuit">State of the API circuit breaker, for the offline banner.</param>
    /// <param name="navigator">Opens the sign-in page.</param>
    public MainPage(
        ApexApiClient api,
        IAccessTokenStore tokens,
        IScanQueries queries,
        IScanRepository scans,
        ScanChangeMonitor monitor,
        CourierSync sync,
        ICircuitStateProvider circuit,
        AppNavigator navigator)
    {
        InitializeComponent();
        (_api, _tokens, _queries, _scans) = (api, tokens, queries, scans);
        (_monitor, _sync, _circuit, _navigator) = (monitor, sync, circuit, navigator);
        ApiAddressLabel.Text = $"API: {api.BaseAddress}";
        ScanTypePicker.ItemsSource = RecordableScanTypes;
        ScanTypePicker.SelectedIndex = 0;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _circuit.StateChanged += OnCircuitStateChanged;
        _monitor.PendingCountChanged += OnPendingCountChanged;
        _sync.SyncCompleted += OnSyncCompleted;
        OfflineBanner.IsVisible = _circuit.IsServerUnavailable;
        ShowPending(await _queries.CountPendingAsync(CancellationToken.None));
        var courierId = AccessTokenClaims.TryGetCourierId(await _tokens.GetAccessTokenAsync(CancellationToken.None));
        CourierLabel.Text = courierId is null ? "Signed in" : $"Signed in as {courierId}";
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        _circuit.StateChanged -= OnCircuitStateChanged;
        _monitor.PendingCountChanged -= OnPendingCountChanged;
        _sync.SyncCompleted -= OnSyncCompleted;
        base.OnDisappearing();
    }

    private void OnCircuitStateChanged(object? sender, CircuitState state) =>
        Dispatcher.Dispatch(() => OfflineBanner.IsVisible = _circuit.IsServerUnavailable);

    // Raised by the Couchbase listener on a background thread.
    private void OnPendingCountChanged(object? sender, int pending) => Dispatcher.Dispatch(() => ShowPending(pending));

    private void OnSyncCompleted(object? sender, OutboxResult result) =>
        Dispatcher.Dispatch(() => ShowSyncResult(result));

    private async void OnSyncClicked(object? sender, EventArgs e)
    {
        SyncButton.IsEnabled = false;
        SyncStatusLabel.TextColor = OkColor;
        SyncStatusLabel.Text = "Syncing…";
        var result = await _sync.SyncNowAsync("button", CancellationToken.None);
        SyncButton.IsEnabled = true;
        if (result == OutboxResult.Empty)
        {
            SyncStatusLabel.Text = "Nothing to upload (or a sync is already running).";
        }
    }

    private async void OnSaveScanClicked(object? sender, EventArgs e)
    {
        var courierId = AccessTokenClaims.TryGetCourierId(await _tokens.GetAccessTokenAsync(CancellationToken.None));
        if (courierId is null)
        {
            await _navigator.ShowLoginAsync("Sign in before recording scans.");
            return;
        }

        try
        {
            var scan = ShipmentScan.CreateNew(
                (ScanTrackingEntry.Text ?? string.Empty).Trim(),
                (ScanType)(ScanTypePicker.SelectedItem ?? ScanType.Pickup),
                courierId,
                LocationEntry.Text ?? string.Empty,
                NotesEntry.Text,
                TimeProvider.System);
            await _scans.CreateAsync(scan, CancellationToken.None);
            ShowScanStatus($"Saved on this device as Pending ({scan.TrackingNumber}, {scan.ScanType}).", OkColor);
            NotesEntry.Text = string.Empty;
            _sync.SyncInBackground("new scan");
        }
        catch (ArgumentException ex)
        {
            ShowScanStatus(ex.Message, ErrorColor);
        }
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        _tokens.Clear();
        await _navigator.ShowLoginAsync();
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
            ? string.Create(
                CultureInfo.CurrentCulture,
                $"{scan.ScanType} at {scan.Location}, {scan.ScannedAtUtc.ToLocalTime():g}")
            : "No scans yet";
        return $"{shipment.TrackingNumber}: {shipment.Status}\nTo {shipment.Recipient.City}\nLatest: {latest}";
    }

    private void ShowPending(int pending) =>
        PendingLabel.Text = string.Create(CultureInfo.CurrentCulture, $"Pending scans on this device: {pending}");

    private void ShowSyncResult(OutboxResult result)
    {
        var text = string.Create(
            CultureInfo.CurrentCulture,
            $"Last sync {DateTime.Now:t}: {result.Synced} synced, {result.Failed} failed, {result.Deferred} waiting.");
        SyncStatusLabel.TextColor = result.Deferred > 0 || result.Failed > 0 ? ErrorColor : OkColor;
        SyncStatusLabel.Text = result.StopReason is null ? text : $"{text}\n{result.StopReason}";
    }

    private void ShowScanStatus(string text, Color color)
    {
        ScanStatusLabel.Text = text;
        ScanStatusLabel.TextColor = color;
    }

    private void SetBusy(bool busy)
    {
        Busy.IsVisible = busy;
        Busy.IsRunning = busy;
        LookupButton.IsEnabled = !busy;
    }
}
