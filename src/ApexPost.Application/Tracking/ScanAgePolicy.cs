using System.Globalization;
using Microsoft.Extensions.Options;

namespace ApexPost.Application.Tracking;

/// <summary>
/// Refuses a scan that reaches the server too long after it was made (<see cref="TrackingOptions.MaxScanAgeHours"/>):
/// a scan from a device that was offline for days would move the shipment long after the parcel moved on.
/// The limit and the reason are read once; the host validates the limit at start-up.
/// </summary>
public sealed class ScanAgePolicy(IOptions<TrackingOptions> options)
{
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(options.Value.MaxScanAgeHours);

    private readonly string _reason = string.Create(
        CultureInfo.InvariantCulture,
        $"Scan is older than {options.Value.MaxScanAgeHours} hours.");

    /// <summary>
    /// The rejection reason when <paramref name="scannedAtUtc"/> is more than the limit before
    /// <paramref name="uploadedAtUtc"/>; <see langword="null"/> when the scan is young enough (exactly the limit is
    /// still accepted).
    /// </summary>
    public string? RejectionFor(DateTime scannedAtUtc, DateTime uploadedAtUtc) =>
        uploadedAtUtc - scannedAtUtc > _maxAge ? _reason : null;
}
