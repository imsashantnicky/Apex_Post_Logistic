using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Outbox;

namespace ApexPost.Courier.Harness;

/// <summary>
/// Fictional scans for demonstrations: couriers CR-101 and CR-102, shipments APX-10000001 to APX-10000006 and one
/// scan of <see cref="FakeShipmentUploader.DemoFailureTrackingNumber"/>, whose upload always fails.
/// </summary>
internal static class DemoData
{
    /// <summary>The first demo tracking number; when it already has scans the demo data is not added again.</summary>
    public const string FirstTrackingNumber = "APX-10000001";

    private const string CourierA = "CR-101";
    private const string CourierB = "CR-102";
    private const string MapleDepot = "Maple Depot";
    private const string NorthHub = "North Hub";

    // Five minutes apart, oldest first, so lists and uploads show a clear order.
    private static readonly TimeSpan Spacing = TimeSpan.FromMinutes(5);

    private static readonly (string Tracking, ScanType Type, string Courier, string Location, string? Notes)[] Scans =
    [
        (FirstTrackingNumber, ScanType.Pickup, CourierA, MapleDepot, null),
        ("APX-10000002", ScanType.Pickup, CourierA, MapleDepot, "Fragile"),
        ("APX-10000003", ScanType.Pickup, CourierB, NorthHub, null),
        (FirstTrackingNumber, ScanType.InTransit, CourierA, NorthHub, null),
        ("APX-10000004", ScanType.InTransit, CourierB, NorthHub, null),
        ("APX-10000005", ScanType.Delivered, CourierA, MapleDepot, "Left with reception"),
        ("APX-10000006", ScanType.Failed, CourierB, MapleDepot, "Recipient not at home"),
        (FakeShipmentUploader.DemoFailureTrackingNumber, ScanType.Pickup, CourierB, NorthHub, "Upload always fails"),
    ];

    /// <summary>Adds the demo scans as Pending, unless they are already present.</summary>
    /// <returns>The number of scans added; 0 when the demo data was already there.</returns>
    public static async Task<int> SeedAsync(
        IScanRepository repository, IScanQueries queries, TimeProvider timeProvider, CancellationToken ct)
    {
        if ((await queries.GetByTrackingNumberAsync(FirstTrackingNumber, ct)).Count > 0)
        {
            return 0;
        }

        var first = timeProvider.GetUtcNow() - (Spacing * Scans.Length);
        for (var i = 0; i < Scans.Length; i++)
        {
            var (tracking, type, courier, location, notes) = Scans[i];
            var scan = ShipmentScan.Restore(
                ScanIds.New(), tracking, type, courier, location, first + (Spacing * i), notes, SyncStatus.Pending);
            await repository.CreateAsync(scan, ct);
        }

        return Scans.Length;
    }
}
