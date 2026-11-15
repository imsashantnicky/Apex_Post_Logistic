using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.Offline.Tests.Support;

/// <summary>Builds fictional scans with an explicit time, so ordering can be asserted.</summary>
internal static class TestScans
{
    public static readonly DateTimeOffset Start = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    public static ShipmentScan At(
        int minutes,
        string trackingNumber = "APX-10000001",
        ScanType scanType = ScanType.Pickup,
        string courierId = "CR-101",
        SyncStatus syncStatus = SyncStatus.Pending) =>
        ShipmentScan.Restore(
            ScanIds.New(), trackingNumber, scanType, courierId, "Maple Depot", Start.AddMinutes(minutes), null,
            syncStatus);
}
