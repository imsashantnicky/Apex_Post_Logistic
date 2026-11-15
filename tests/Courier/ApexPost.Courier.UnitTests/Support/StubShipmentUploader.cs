using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.UnitTests.Support;

/// <summary>An uploader whose behaviour each test supplies; records every scan it was asked to upload.</summary>
internal sealed class StubShipmentUploader(Func<ShipmentScan, CancellationToken, Task<UploadResult>> upload)
    : IShipmentUploader
{
    private readonly List<string> _uploadedIds = [];

    public static UploadResult Accepted { get; } = new(true, null);

    public IReadOnlyList<string> UploadedIds
    {
        get
        {
            lock (_uploadedIds)
            {
                return [.. _uploadedIds];
            }
        }
    }

    public static StubShipmentUploader AcceptAll() => new((_, _) => Task.FromResult(Accepted));

    public Task<UploadResult> UploadAsync(ShipmentScan scan, CancellationToken ct)
    {
        lock (_uploadedIds)
        {
            _uploadedIds.Add(scan.Id);
        }

        return upload(scan, ct);
    }
}
