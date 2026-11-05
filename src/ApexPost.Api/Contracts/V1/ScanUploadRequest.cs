using System.ComponentModel.DataAnnotations;
using ApexPost.Application.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>The body of POST /api/shipments/{trackingNumber}/scans: a batch of device scans, safe to send again.</summary>
public sealed class ScanUploadRequest
{
    /// <summary>1 to 100 scans of this shipment.</summary>
    /// <example>
    /// [{ "scanId": "scan::0f8fad5b-d9cb-469f-a165-70867728950e", "trackingNumber": "APX-20000001",
    ///    "scanType": 1, "courierId": "CR-101", "location": "Harbour Point depot",
    ///    "scannedAtUtc": "2026-11-05T09:30:00.0000000Z", "notes": "Collected from reception." }]
    /// </example>
    [Required]
    [Length(1, TrackingLimits.MaximumScansPerUpload)]
    public IReadOnlyList<ScanItem> Scans { get; init; } = [];
}
