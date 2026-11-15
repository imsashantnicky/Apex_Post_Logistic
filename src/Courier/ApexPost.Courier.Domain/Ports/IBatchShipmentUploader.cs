using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.Domain.Ports;

/// <summary>
/// Sends several scans of one shipment in one request. A separate port, so <see cref="IShipmentUploader"/> stays as
/// it was in Part A: the fake uploader implements only the single-scan port, the HTTP uploader implements both.
/// </summary>
public interface IBatchShipmentUploader : IShipmentUploader
{
    /// <summary>The largest number of scans one call may send.</summary>
    int MaxBatchSize { get; }

    /// <summary>Uploads <paramref name="scans"/>, which all belong to <paramref name="trackingNumber"/>.</summary>
    /// <param name="trackingNumber">The shipment the scans belong to.</param>
    /// <param name="scans">One to <see cref="MaxBatchSize"/> scans of that shipment.</param>
    /// <param name="ct">Cancels the upload.</param>
    /// <returns>The outcome of the batch; never an exception for a server or network problem.</returns>
    Task<BatchUploadResult> UploadBatchAsync(
        string trackingNumber,
        IReadOnlyList<ShipmentScan> scans,
        CancellationToken ct);
}
