using ApexPost.Application.Tracking;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Tracking.UnitTests.Support;
using Microsoft.Extensions.Options;
using CourierScanType = ApexPost.Courier.Domain.Enums.ScanType;
using ServerScanType = ApexPost.Domain.Tracking.ScanType;

namespace ApexPost.Specs.Support;

/// <summary>
/// The device's <see cref="IBatchShipmentUploader"/> port wired straight to the real
/// <see cref="ShipmentTrackingService"/> on in-memory repositories, with no HTTP in between. Each call builds a new
/// service, as each request would. <see cref="IsOnline"/> and <see cref="LoseNextAnswer"/> play the network.
/// </summary>
internal sealed class InProcessShipmentUploader(
    InMemoryTracking server,
    string courierId,
    TimeProvider clock) : IBatchShipmentUploader
{
    private const string NoConnection = "No connection.";
    private const string AnswerLost = "The answer was lost on the way back.";

    private readonly List<RejectedScan> _rejected = [];

    /// <summary>False while the device has no connection: every call then fails as retryable.</summary>
    public bool IsOnline { get; set; }

    /// <summary>When true, the next call reaches the server but its answer never arrives (retryable).</summary>
    public bool LoseNextAnswer { get; set; }

    /// <summary>Duplicates the server skipped over every answered call of the scenario.</summary>
    public int DuplicatesSkipped { get; private set; }

    /// <summary>Every scan the server refused in an answered call, with its reason.</summary>
    public IReadOnlyList<RejectedScan> Rejected => _rejected;

    public int MaxBatchSize => TrackingLimits.MaximumScansPerUpload;

    public async Task<BatchUploadResult> UploadBatchAsync(
        string trackingNumber,
        IReadOnlyList<ShipmentScan> scans,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scans);
        if (!IsOnline)
        {
            return BatchUploadResult.Retryable(NoConnection);
        }

        var result = await NewService().UploadScansAsync([.. scans.Select(ToUpload)], ct).ConfigureAwait(false);
        if (LoseNextAnswer)
        {
            LoseNextAnswer = false;
            return BatchUploadResult.Retryable(AnswerLost);
        }

        DuplicatesSkipped += result.DuplicatesSkipped;
        _rejected.AddRange(result.Rejected);
        return BatchUploadResult.Completed(
            result.Rejected.ToDictionary(scan => scan.ScanId, scan => scan.Reason, StringComparer.Ordinal));
    }

    public async Task<UploadResult> UploadAsync(ShipmentScan scan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scan);
        var result = await UploadBatchAsync(scan.TrackingNumber, [scan], ct).ConfigureAwait(false);
        var error = result.Error ?? result.Rejected.GetValueOrDefault(scan.Id);
        return new UploadResult(error is null, error);
    }

    private ShipmentTrackingService NewService() => new(
        server,
        server,
        server,
        new SpecCourier(courierId),
        clock,
        new ScanAgePolicy(Options.Create(new TrackingOptions())));

    private static ScanUpload ToUpload(ShipmentScan scan) => new(
        scan.Id,
        scan.TrackingNumber,
        ToServer(scan.ScanType),
        scan.CourierId,
        scan.Location,
        scan.ScannedAtUtc.UtcDateTime,
        scan.Notes);

    private static ServerScanType ToServer(CourierScanType type) => type switch
    {
        CourierScanType.Pickup => ServerScanType.Pickup,
        CourierScanType.InTransit => ServerScanType.InTransit,
        CourierScanType.Delivered => ServerScanType.Delivered,
        CourierScanType.Failed => ServerScanType.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown scan type."),
    };
}
