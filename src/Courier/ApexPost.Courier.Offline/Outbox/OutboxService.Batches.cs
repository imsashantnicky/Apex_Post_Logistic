using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Offline.Outbox;

/// <summary>
/// The batch path of the outbox, used when the uploader implements <see cref="IBatchShipmentUploader"/>.
/// </summary>
public sealed partial class OutboxService
{
    /// <summary>Most scans sent in one request. The API accepts 100; 20 keeps a request small on a weak link.</summary>
    public const int BatchSize = 20;

    /// <summary>
    /// Splits scans into batches: grouped per tracking number (the API takes the scans of one shipment per call),
    /// groups in the order of their oldest scan, scans oldest first, at most <paramref name="batchSize"/> each.
    /// </summary>
    /// <param name="scans">The scans to send.</param>
    /// <param name="batchSize">Largest batch, at least 1.</param>
    public static IReadOnlyList<IReadOnlyList<ShipmentScan>> BuildBatches(
        IEnumerable<ShipmentScan> scans,
        int batchSize)
    {
        ArgumentNullException.ThrowIfNull(scans);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        return [.. scans
            .GroupBy(scan => scan.TrackingNumber, StringComparer.Ordinal)
            .Select(group => group.OrderBy(scan => scan.ScannedAtUtc).ToList())
            .OrderBy(group => group[0].ScannedAtUtc)
            .SelectMany(group => group.Chunk(batchSize))
            .Select(IReadOnlyList<ShipmentScan> (chunk) => chunk)];
    }

    private async Task<OutboxResult> UploadInBatchesAsync(
        IBatchShipmentUploader uploader,
        IReadOnlyList<ShipmentScan> scans,
        CancellationToken ct)
    {
        int synced = 0, failed = 0;
        foreach (var batch in BuildBatches(scans, Math.Min(BatchSize, uploader.MaxBatchSize)))
        {
            ct.ThrowIfCancellationRequested();
            var result = await SendBatchAsync(uploader, batch, ct).ConfigureAwait(false);
            if (result.Outcome is BatchUploadOutcome.Retryable or BatchUploadOutcome.Unauthorized)
            {
                // The server is unreachable or wants a new sign-in: every later batch would fail the same way.
                var deferred = scans.Count - synced - failed;
                LogRunStopped(_logger, result.Outcome, deferred, result.Error ?? "unknown");
                return new OutboxResult(scans.Count, synced, failed)
                {
                    Deferred = deferred,
                    SignInRequired = result.Outcome == BatchUploadOutcome.Unauthorized,
                    StopReason = result.Error,
                };
            }

            var (batchSynced, batchFailed) = await ApplyAsync(batch, result).ConfigureAwait(false);
            synced += batchSynced;
            failed += batchFailed;
        }

        return new OutboxResult(scans.Count, synced, failed);
    }

    private static async Task<BatchUploadResult> SendBatchAsync(
        IBatchShipmentUploader uploader,
        IReadOnlyList<ShipmentScan> batch,
        CancellationToken ct)
    {
        try
        {
            return await uploader.UploadBatchAsync(batch[0].TrackingNumber, batch, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // An unexpected error says nothing about the scans themselves, so they wait for the next run.
            return BatchUploadResult.Retryable(ex.GetType().Name);
        }
    }

    // Completed: rejected ids become Failed, every other scan (accepted or duplicate) Synced. Refused: all Failed.
    private async Task<(int Synced, int Failed)> ApplyAsync(IReadOnlyList<ShipmentScan> batch, BatchUploadResult result)
    {
        int synced = 0, failed = 0;
        foreach (var scan in batch)
        {
            var reason = result.Outcome == BatchUploadOutcome.Refused
                ? result.Error ?? "refused"
                : result.Rejected.GetValueOrDefault(scan.Id);
            if (reason is null)
            {
                await SaveStatusAsync(scan.Id, SyncStatus.Synced).ConfigureAwait(false);
                synced++;
            }
            else
            {
                LogUploadFailed(_logger, scan.Id, scan.TrackingNumber, reason);
                await SaveStatusAsync(scan.Id, SyncStatus.Failed).ConfigureAwait(false);
                failed++;
            }
        }

        return (synced, failed);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Outbox run stopped ({Outcome}): {Deferred} scans wait for the next run. {Reason}")]
    private static partial void LogRunStopped(ILogger logger, BatchUploadOutcome outcome, int deferred, string reason);
}
