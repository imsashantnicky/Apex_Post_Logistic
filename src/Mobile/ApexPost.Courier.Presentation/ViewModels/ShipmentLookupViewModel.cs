using System.Globalization;
using ApexPost.Courier.ApiClient;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.Presentation.Errors;
using ApexPost.Courier.Presentation.Mvvm;

namespace ApexPost.Courier.Presentation.ViewModels;

/// <summary>
/// "Find a shipment" card: one <c>GET /api/v2/shipments/{trackingNumber}</c>. A ProblemDetails answer (404,
/// 401, 5xx …) is shown as the friendly text of the client plus the trace id as a reference.
/// </summary>
public sealed class ShipmentLookupViewModel : ObservableObject
{
    private readonly ApexApiClient _api;
    private string _trackingNumber = string.Empty;
    private string? _result;
    private bool _resultIsError;

    /// <summary>Creates the view model.</summary>
    /// <param name="api">Typed API client.</param>
    public ShipmentLookupViewModel(ApexApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
        LookupCommand = new AsyncCommand(LookupAsync);
    }

    /// <summary>Tracking number to look up.</summary>
    public string TrackingNumber
    {
        get => _trackingNumber;
        set => SetProperty(ref _trackingNumber, value ?? string.Empty);
    }

    /// <summary>The shipment in words, or the error text.</summary>
    public string? Result
    {
        get => _result;
        private set => SetProperty(ref _result, value);
    }

    /// <summary>Whether <see cref="Result"/> is an error.</summary>
    public bool ResultIsError
    {
        get => _resultIsError;
        private set => SetProperty(ref _resultIsError, value);
    }

    /// <summary>Asks the API for the shipment.</summary>
    public AsyncCommand LookupCommand { get; }

    private async Task LookupAsync()
    {
        TrackingNumber = TrackingNumber.Trim().ToUpperInvariant();
        Result = "Looking up…";
        ResultIsError = false;
        var result = await _api.GetShipmentAsync(TrackingNumber, CancellationToken.None).ConfigureAwait(true);
        Result = result.IsSuccess ? Describe(result.Value) : FriendlyError.Describe(result.Failure);
        ResultIsError = !result.IsSuccess;
    }

    private static string Describe(ShipmentResponse shipment)
    {
        var latest = shipment.LatestScan is { } scan
            ? string.Create(
                CultureInfo.CurrentCulture,
                $"{scan.ScanType} at {scan.Location}, {scan.ScannedAtUtc.ToLocalTime():g}")
            : "No scans yet";
        return $"{shipment.TrackingNumber}: {shipment.Status}{Environment.NewLine}"
            + $"To {shipment.Recipient.City}{Environment.NewLine}Latest: {latest}";
    }
}
