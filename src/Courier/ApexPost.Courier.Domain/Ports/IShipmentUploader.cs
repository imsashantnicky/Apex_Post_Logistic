using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.Domain.Ports;

/// <summary>
/// Sends a scan to the server. Part A uses an in-memory fake; Part D replaces it with an HTTP uploader.
/// </summary>
public interface IShipmentUploader
{
    /// <summary>Uploads one scan.</summary>
    /// <returns>A failed result, not an exception, when the server rejects the scan or cannot be reached.</returns>
    Task<UploadResult> UploadAsync(ShipmentScan scan, CancellationToken ct);
}
