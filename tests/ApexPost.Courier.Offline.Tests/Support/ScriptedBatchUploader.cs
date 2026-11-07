using System.Collections.Concurrent;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.Offline.Tests.Support;

/// <summary>
/// A batch uploader that records every batch and answers batch N (1-based) with the scripted result.
/// </summary>
internal sealed class ScriptedBatchUploader(Func<int, IReadOnlyList<ShipmentScan>, BatchUploadResult> answer)
    : IBatchShipmentUploader
{
    public ConcurrentQueue<IReadOnlyList<ShipmentScan>> Batches { get; } = new();

    public int MaxBatchSize { get; init; } = 100;

    public static ScriptedBatchUploader AcceptingAll() =>
        new((_, _) => BatchUploadResult.Completed(new Dictionary<string, string>()));

    public Task<UploadResult> UploadAsync(ShipmentScan scan, CancellationToken ct) =>
        throw new InvalidOperationException("The outbox must use the batch method of a batch uploader.");

    public Task<BatchUploadResult> UploadBatchAsync(
        string trackingNumber,
        IReadOnlyList<ShipmentScan> scans,
        CancellationToken ct)
    {
        Assert.All(scans, scan => Assert.Equal(trackingNumber, scan.TrackingNumber));
        Batches.Enqueue(scans);
        return Task.FromResult(answer(Batches.Count, scans));
    }
}
