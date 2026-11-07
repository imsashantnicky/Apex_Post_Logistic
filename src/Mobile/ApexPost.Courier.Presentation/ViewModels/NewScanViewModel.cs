using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Presentation.Mvvm;
using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Presentation.ViewModels;

/// <summary>
/// New Scan page. The input is checked against <see cref="ScanRules"/> before anything is saved, the courier id
/// comes from the token (never typed), and the scan is saved on the device as Pending first; an upload starts in
/// the background afterwards, so recording never waits for the network.
/// </summary>
public sealed class NewScanViewModel : ObservableObject
{
    /// <summary>Error under the location field.</summary>
    public const string LocationRequired = "Enter where the parcel was scanned.";

    /// <summary>Shown when the stored token has no courier id.</summary>
    public const string SignInFirst = "Sign in before recording scans.";

    /// <summary>Shown when a field is invalid.</summary>
    public const string CheckFields = "Check the highlighted fields.";

    private const string NewScanTrigger = "new scan";

    private readonly IScanRepository _scans;
    private readonly IAccessTokenStore _tokens;
    private readonly IAppNavigator _navigator;
    private readonly ISyncRunner _sync;
    private readonly LoginViewModel _login;
    private readonly TimeProvider _time;
    private string _trackingNumber = string.Empty;
    private ScanType _scanType = ScanType.Pickup;
    private string _location = string.Empty;
    private string _notes = string.Empty;
    private string? _trackingNumberError;
    private string? _locationError;
    private string? _status;
    private bool _statusIsError;

    /// <summary>Creates the view model.</summary>
    /// <param name="scans">Local scan store.</param>
    /// <param name="tokens">Secure token store (source of the courier id).</param>
    /// <param name="navigator">Opens sign-in when no courier is signed in.</param>
    /// <param name="sync">Starts an upload after a save.</param>
    /// <param name="login">Sign-in view model, to show why the courier was sent there.</param>
    /// <param name="time">Clock for the scan time.</param>
    /// <param name="lookup">Shipment look-up shown on the same page.</param>
    /// <param name="banner">Global offline banner.</param>
    public NewScanViewModel(
        IScanRepository scans,
        IAccessTokenStore tokens,
        IAppNavigator navigator,
        ISyncRunner sync,
        LoginViewModel login,
        TimeProvider time,
        ShipmentLookupViewModel lookup,
        ConnectionBannerViewModel banner)
    {
        ArgumentNullException.ThrowIfNull(scans);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(banner);
        (_scans, _tokens, _navigator, _sync, _login, _time) = (scans, tokens, navigator, sync, login, time);
        Lookup = lookup;
        Banner = banner;
        SaveCommand = new AsyncCommand(SaveAsync);
    }

    /// <summary>
    /// Scan types the page offers. Delivered is left out until the device stores the recipient signature the API
    /// requires for it.
    /// </summary>
    public IReadOnlyList<ScanType> ScanTypes { get; } = [ScanType.Pickup, ScanType.InTransit, ScanType.Failed];

    /// <summary>Global offline banner.</summary>
    public ConnectionBannerViewModel Banner { get; }

    /// <summary>Shipment look-up card.</summary>
    public ShipmentLookupViewModel Lookup { get; }

    /// <summary>Tracking number, <c>APX-</c> plus eight digits (lower case is accepted and upper-cased).</summary>
    public string TrackingNumber
    {
        get => _trackingNumber;
        set => SetProperty(ref _trackingNumber, value ?? string.Empty);
    }

    /// <summary>Selected scan type.</summary>
    public ScanType SelectedScanType
    {
        get => _scanType;
        set => SetProperty(ref _scanType, value);
    }

    /// <summary>Where the parcel was scanned.</summary>
    public string Location
    {
        get => _location;
        set => SetProperty(ref _location, value ?? string.Empty);
    }

    /// <summary>Optional notes.</summary>
    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value ?? string.Empty);
    }

    /// <summary>Error under the tracking number field, or <see langword="null"/>.</summary>
    public string? TrackingNumberError
    {
        get => _trackingNumberError;
        private set
        {
            if (SetProperty(ref _trackingNumberError, value))
            {
                OnPropertyChanged(nameof(HasTrackingNumberError));
            }
        }
    }

    /// <summary>Whether <see cref="TrackingNumberError"/> is shown.</summary>
    public bool HasTrackingNumberError => _trackingNumberError is not null;

    /// <summary>Error under the location field, or <see langword="null"/>.</summary>
    public string? LocationError
    {
        get => _locationError;
        private set
        {
            if (SetProperty(ref _locationError, value))
            {
                OnPropertyChanged(nameof(HasLocationError));
            }
        }
    }

    /// <summary>Whether <see cref="LocationError"/> is shown.</summary>
    public bool HasLocationError => _locationError is not null;

    /// <summary>Result of the last save.</summary>
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>Whether <see cref="Status"/> is an error.</summary>
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    /// <summary>Validates the input and saves the scan on the device as Pending.</summary>
    public AsyncCommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        if (!Validate())
        {
            ShowStatus(CheckFields, isError: true);
            return;
        }

        var token = await _tokens.GetAccessTokenAsync(CancellationToken.None).ConfigureAwait(true);
        var courierId = AccessTokenClaims.TryGetCourierId(token);
        if (courierId is null)
        {
            _login.ShowError(SignInFirst);
            await _navigator.ShowLoginAsync().ConfigureAwait(true);
            return;
        }

        try
        {
            var scan = ShipmentScan.CreateNew(TrackingNumber, SelectedScanType, courierId, Location, Notes, _time);
            await _scans.CreateAsync(scan, CancellationToken.None).ConfigureAwait(true);
            ShowStatus($"Saved on this device as Pending ({scan.TrackingNumber}, {scan.ScanType}).", isError: false);
            Notes = string.Empty;
            _sync.SyncInBackground(NewScanTrigger);
        }
        catch (ArgumentException ex)
        {
            // The checks above mirror the domain rules, so this is only reached if the two drift apart.
            ShowStatus(ex.Message, isError: true);
        }
    }

    private bool Validate()
    {
        TrackingNumber = TrackingNumber.Trim().ToUpperInvariant();
        Location = Location.Trim();
        TrackingNumberError = ScanRules.IsValidTrackingNumber(TrackingNumber)
            ? null
            : FailureMessages.InvalidTrackingNumber;
        LocationError = Location.Length > 0 ? null : LocationRequired;
        return TrackingNumberError is null && LocationError is null;
    }

    private void ShowStatus(string text, bool isError)
    {
        Status = text;
        StatusIsError = isError;
    }
}
