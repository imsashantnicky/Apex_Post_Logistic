using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Scans;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Offline.Outbox;

/// <summary>
/// Uploads the scans that are waiting on the device and records the outcome of each upload.
/// </summary>
/// <remarks>
/// The offline rule: every scan is saved locally first as <see cref="SyncStatus.Pending"/>, and only the outbox
/// changes its sync status afterwards (<see cref="SyncStatus.Synced"/> or <see cref="SyncStatus.Failed"/>).
/// Recording a scan therefore never depends on the network.
/// </remarks>
public sealed partial class OutboxService : IDisposable
{
    private readonly IScanQueries _queries;
    private readonly CouchbaseScanRepository _statusUpdater;
    private readonly IShipmentUploader _uploader;
    private readonly ILogger<OutboxService> _logger;

    // Single-flight: a second run that starts while one is busy returns at once instead of uploading twice.
    private readonly SemaphoreSlim _singleFlight = new(1, 1);

    /// <summary>Creates the outbox.</summary>
    /// <param name="queries">Finds the scans to upload.</param>
    /// <param name="statusUpdater">Saves the new sync status after each upload attempt.</param>
    /// <param name="uploader">Sends a scan to the server (a fake in Part A, HTTP in Part D).</param>
    /// <param name="logger">Logs each run and every failed upload.</param>
    public OutboxService(
        IScanQueries queries,
        CouchbaseScanRepository statusUpdater,
        IShipmentUploader uploader,
        ILogger<OutboxService> logger)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(statusUpdater);
        ArgumentNullException.ThrowIfNull(uploader);
        ArgumentNullException.ThrowIfNull(logger);
        _queries = queries;
        _statusUpdater = statusUpdater;
        _uploader = uploader;
        _logger = logger;
    }

    /// <summary>
    /// Uploads every pending scan, oldest first. An accepted scan becomes Synced; a rejected scan or an upload
    /// that throws becomes Failed. One bad scan never stops the others.
    /// </summary>
    /// <param name="includeFailed">Also retry scans whose earlier upload failed.</param>
    /// <param name="ct">Stops the run before the next scan. Scans not attempted yet keep their status.</param>
    /// <returns>The counts of this run, or <see cref="OutboxResult.Empty"/> when another run is busy.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    public async Task<OutboxResult> ProcessPendingAsync(bool includeFailed = false, CancellationToken ct = default)
    {
        if (!await _singleFlight.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false))
        {
            LogRunSkipped(_logger);
            return OutboxResult.Empty;
        }

        try
        {
            var scans = await LoadScansAsync(includeFailed, ct).ConfigureAwait(false);
            int synced = 0, failed = 0;
            foreach (var scan in scans)
            {
                ct.ThrowIfCancellationRequested();
                if (await UploadOneAsync(scan, ct).ConfigureAwait(false))
                {
                    synced++;
                }
                else
                {
                    failed++;
                }
            }

            LogRunFinished(_logger, scans.Count, synced, failed);
            return new OutboxResult(scans.Count, synced, failed);
        }
        finally
        {
            _singleFlight.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _singleFlight.Dispose();

    private async Task<IReadOnlyList<ShipmentScan>> LoadScansAsync(bool includeFailed, CancellationToken ct)
    {
        var pending = await _queries.GetPendingAsync(ct).ConfigureAwait(false);
        if (!includeFailed)
        {
            return pending;
        }

        var failed = await _queries.GetFailedAsync(ct).ConfigureAwait(false);
        return [.. pending.Concat(failed).OrderBy(scan => scan.ScannedAtUtc)];
    }

    private async Task<bool> UploadOneAsync(ShipmentScan scan, CancellationToken ct)
    {
        UploadResult result;
        try
        {
            result = await _uploader.UploadAsync(scan, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Any upload error is a failed attempt for this scan only; the run continues with the next one.
            result = new UploadResult(false, ex.GetType().Name);
        }

        var status = result.Success ? SyncStatus.Synced : SyncStatus.Failed;
        if (!result.Success)
        {
            LogUploadFailed(_logger, scan.Id, scan.TrackingNumber, result.Error ?? "unknown");
        }

        // The upload has already happened, so the outcome is saved even if cancellation arrives now.
        if (!await _statusUpdater.UpdateSyncStatusAsync(scan.Id, status, CancellationToken.None).ConfigureAwait(false))
        {
            LogScanMissing(_logger, scan.Id);
        }

        return result.Success;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox run skipped: another run is in progress")]
    private static partial void LogRunSkipped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Outbox run finished: {Attempted} attempted, {Synced} synced, {Failed} failed")]
    private static partial void LogRunFinished(ILogger logger, int attempted, int synced, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload of scan {ScanId} ({TrackingNumber}) failed: {Reason}")]
    private static partial void LogUploadFailed(ILogger logger, string scanId, string trackingNumber, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scan {ScanId} was deleted before its sync status was saved")]
    private static partial void LogScanMissing(ILogger logger, string scanId);
}
