using System.Globalization;
using ApexPost.Courier.ApiClient;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.Domain.Ports;

namespace ApexPost.Courier;

/// <summary>
/// Start page: stores the courier token in SecureStorage, looks up a shipment through the typed client and shows
/// the number of scans still waiting on the device. Every API failure is shown as its friendly message.
/// </summary>
public partial class MainPage : ContentPage
{
    private static readonly Color ErrorColor = Color.FromArgb("#B3261E");
    private static readonly Color OkColor = Color.FromArgb("#1B7F4B");

    private readonly ApexApiClient _api;
    private readonly IAccessTokenStore _tokens;
    private readonly IScanQueries _scans;

    /// <summary>Creates the page with its services.</summary>
    /// <param name="api">Typed API client.</param>
    /// <param name="tokens">Secure token store.</param>
    /// <param name="scans">Local scan queries.</param>
    public MainPage(ApexApiClient api, IAccessTokenStore tokens, IScanQueries scans)
    {
        InitializeComponent();
        _api = api;
        _tokens = tokens;
        _scans = scans;
        ApiAddressLabel.Text = $"API: {api.BaseAddress}";
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var pending = await _scans.GetPendingAsync(CancellationToken.None);
        PendingLabel.Text = string.Create(CultureInfo.CurrentCulture, $"Pending scans on this device: {pending.Count}");
        var token = await _tokens.GetAccessTokenAsync(CancellationToken.None);
        ShowTokenStatus(string.IsNullOrEmpty(token) ? "Not signed in." : "Token stored securely.", OkColor);
    }

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
