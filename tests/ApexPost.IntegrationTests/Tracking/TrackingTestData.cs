using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;

namespace ApexPost.IntegrationTests.Tracking;

/// <summary>Fictional shipments and scans for the tracking SQL tests, at a fixed time (no wall clock).</summary>
internal static class TrackingTestData
{
    public const string CourierId = "CR-101";

    public static readonly DateTimeOffset Now = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    public static Recipient Recipient() => new("Mia Example", "+44 20 7946 0001", "1 Harbour Road", "Harbour Point");

    public static ShipmentCommand Command() =>
        new("Mia Example", "+44 20 7946 0001", "1 Harbour Road", "Harbour Point", 2.5m);

    public static TrackedShipment Shipment(string trackingNumber) =>
        TrackedShipment.Create(trackingNumber, Recipient(), 2.5m, Now.UtcDateTime);

    public static ScanUpload Scan(ScanType type, string trackingNumber, int minutesAfterNow = 0) =>
        new(
            TrackingFormats.ScanIdPrefix + Guid.NewGuid().ToString("D"),
            trackingNumber,
            type,
            CourierId,
            "Central Hub",
            Now.UtcDateTime.AddMinutes(minutesAfterNow),
            null);
}
