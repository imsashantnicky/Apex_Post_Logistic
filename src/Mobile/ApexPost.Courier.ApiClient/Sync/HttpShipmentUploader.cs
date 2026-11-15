using System.Net;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.ApiClient.Sync;

/// <summary>
/// The real uploader of the Part A ports: sends scans to <c>POST /api/v2/shipments/{trackingNumber}/scans</c>
/// through <see cref="ApexApiClient"/>, so every upload has the bearer token, the retries and the circuit breaker.
/// </summary>
/// <remarks>
/// The uploader is a singleton (the outbox is), so it asks <see cref="IHttpClientFactory"/> for a fresh client per
/// call instead of holding one; the factory keeps rotating the handlers underneath.
/// </remarks>
/// <param name="httpClientFactory">Creates the <see cref="ApexApiClient.HttpClientName"/> client.</param>
public sealed class HttpShipmentUploader(IHttpClientFactory httpClientFactory) : IBatchShipmentUploader
{
    /// <summary>The API's limit per upload request.</summary>
    public const int ApiMaxBatchSize = 100;

    /// <inheritdoc />
    public int MaxBatchSize => ApiMaxBatchSize;

    /// <inheritdoc />
    public async Task<UploadResult> UploadAsync(ShipmentScan scan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scan);
        var result = await UploadBatchAsync(scan.TrackingNumber, [scan], ct).ConfigureAwait(false);
        var error = result.Outcome == BatchUploadOutcome.Completed
            ? result.Rejected.GetValueOrDefault(scan.Id)
            : result.Error;
        return new UploadResult(error is null, error);
    }

    /// <inheritdoc />
    public async Task<BatchUploadResult> UploadBatchAsync(
        string trackingNumber,
        IReadOnlyList<ShipmentScan> scans,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scans);
        ArgumentOutOfRangeException.ThrowIfZero(scans.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(scans.Count, MaxBatchSize);
        if (scans.Any(scan => !string.Equals(scan.TrackingNumber, trackingNumber, StringComparison.Ordinal)))
        {
            throw new ArgumentException("All scans of a batch must have the same tracking number.", nameof(scans));
        }

        var client = new ApexApiClient(httpClientFactory.CreateClient(ApexApiClient.HttpClientName));
        var request = new ScanUploadRequest([.. scans.Select(ToItem)]);
        var result = await client.UploadScansAsync(trackingNumber, request, ct).ConfigureAwait(false);

        return result.IsSuccess
            ? BatchUploadResult.Completed(result.Value.Rejected.ToDictionary(
                rejected => rejected.ScanId,
                rejected => rejected.Reason,
                StringComparer.Ordinal))
            : FromFailure(result.Failure);
    }

    /// <summary>Maps a failed upload to what the outbox does with the scans.</summary>
    /// <param name="failure">The typed failure of the client.</param>
    public static BatchUploadResult FromFailure(ApiFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Kind switch
        {
            ApiFailureKind.Unauthorized => BatchUploadResult.Unauthorized(failure.Message),

            // The batch may not have arrived, or the answer was lost: sending it again is safe (scan ids).
            ApiFailureKind.Network or ApiFailureKind.Timeout or ApiFailureKind.Server
                or ApiFailureKind.ServerUnavailable or ApiFailureKind.InvalidResponse
                => BatchUploadResult.Retryable(failure.Message),
            _ when failure.StatusCode == (int)HttpStatusCode.TooManyRequests
                => BatchUploadResult.Retryable(failure.Message),

            // 400, 403, 404 and other refusals: the same batch would be refused again.
            _ => BatchUploadResult.Refused(failure.Message),
        };
    }

    // The device document has no recipient signature field yet, so a Delivered scan is sent without one.
    private static ScanItem ToItem(ShipmentScan scan) => new(
        scan.Id,
        scan.TrackingNumber,
        scan.ScanType,
        scan.CourierId,
        scan.Location,
        scan.ScannedAtUtc.UtcDateTime,
        scan.Notes,
        RecipientSignatureName: null);
}
