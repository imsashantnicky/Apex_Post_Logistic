using ApexPost.Api.Contracts.V1;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Tests.Support;

/// <summary>Valid fictional requests; each test changes the one field it is about.</summary>
public static class Requests
{
    public const string ScanId = "scan::0f8fad5b-d9cb-469f-a165-70867728950e";

    public static CreateShipmentRequest Create() => new()
    {
        TrackingNumber = "APX-20000099",
        RecipientName = "Ava Example",
        RecipientPhone = "+44 20 7946 0101",
        RecipientAddress = "12 Quay Street",
        RecipientCity = "Harbour Point",
        WeightKg = 2.5m,
    };

    public static UpdateShipmentRequest Update() => new()
    {
        RecipientName = "Ben Sample",
        RecipientPhone = "020 7946 0102",
        RecipientAddress = "4 Mill Lane",
        RecipientCity = "Northgate",
        WeightKg = 1m,
    };

    public static ScanItem Scan(ScanType type = ScanType.Pickup) => new()
    {
        ScanId = ScanId,
        TrackingNumber = "APX-20000001",
        ScanType = type,
        CourierId = "CR-101",
        Location = "Central Hub",
        ScannedAtUtc = FixedTimeProvider.DefaultNow.UtcDateTime.AddMinutes(-30),
        Notes = "Left at reception",
        RecipientSignatureName = type == ScanType.Delivered ? "Ava Example" : null,
    };
}
