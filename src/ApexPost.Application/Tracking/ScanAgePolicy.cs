using Microsoft.Extensions.Options;

namespace ApexPost.Application.Tracking;

/// <summary>
/// Refuses a scan that reaches the server too long after it was made (<see cref="TrackingOptions.MaxScanAgeHours"/>):
/// a scan from a device that was offline for days would move the shipment long after the parcel moved on.
/// </summary>
public sealed class ScanAgePolicy(IOptions<TrackingOptions> options)
{
    private readonly int _maxAgeHours = options.Value.MaxScanAgeHours;

    /// <summary>
    /// The rejection reason when <paramref name="scannedAtUtc"/> is more than the limit before
    /// <paramref name="uploadedAtUtc"/>; <see langword="null"/> when the scan is young enough.
    /// </summary>
    public string? RejectionFor(DateTime scannedAtUtc, DateTime uploadedAtUtc) =>
        uploadedAtUtc - scannedAtUtc > TimeSpan.FromHours(_maxAgeHours)
            ? $"Scan is older than {_maxAgeHours} hours."
            : null;
}
