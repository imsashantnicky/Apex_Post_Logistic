using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;

namespace ApexPost.Tracking.UnitTests.Support;

/// <summary>Fictional shipments and scans.</summary>
internal static class TestData
{
    public const string TrackingNumber = "APX-10000001";
    public const string OtherTrackingNumber = "APX-10000002";
    public const string CourierId = "CR-101";

    public static readonly DateTime Now = FixedTimeProvider.Default.UtcDateTime;

    public static Recipient Recipient() => new("Mia Example", "+44 20 7946 0001", "1 Harbour Road", "Harbour Point");

    public static ShipmentCommand Command(decimal weightKg = 2.5m, string city = "Harbour Point") =>
        new("Mia Example", "+44 20 7946 0001", "1 Harbour Road", city, weightKg);

    public static TrackedShipment Shipment(string trackingNumber = TrackingNumber) =>
        TrackedShipment.Create(trackingNumber, Recipient(), 2.5m, Now);

    public static ScanUpload Scan(
        ScanType type,
        string trackingNumber = TrackingNumber,
        int minutesAfterNow = 0,
        string? scanId = null,
        string courierId = CourierId) =>
        new(
            scanId ?? NewScanId(),
            trackingNumber,
            type,
            courierId,
            "Central Hub",
            Now.AddMinutes(minutesAfterNow),
            null);

    public static string NewScanId() => TrackingFormats.ScanIdPrefix + Guid.NewGuid().ToString("D");
}
