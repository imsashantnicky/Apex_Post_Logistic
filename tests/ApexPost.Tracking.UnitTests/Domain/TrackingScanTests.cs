using ApexPost.Domain.Common;
using ApexPost.Domain.Tracking;
using ApexPost.Tracking.UnitTests.Support;

namespace ApexPost.Tracking.UnitTests.Domain;

public sealed class TrackingScanTests
{
    [Fact]
    public void A_valid_scan_is_recorded_for_its_shipment()
    {
        var shipment = TestData.Shipment();
        var upload = TestData.Scan(ScanType.Pickup);

        var scan = TrackingScan.Record(shipment, upload.Details, TestData.Now);

        Assert.Equal(upload.ScanId, scan.ScanId);
        Assert.Equal(shipment.Id, scan.ShipmentId);
        Assert.Equal(ScanType.Pickup, scan.ScanType);
        Assert.Null(scan.Notes);
    }

    [Theory]
    [InlineData("scan::not-a-guid", TestData.CourierId)]
    [InlineData("8f0e7f62-4bb9-4c34-9a64-1e1a1c2b3d4e", TestData.CourierId)]
    [InlineData("scan::8f0e7f62-4bb9-4c34-9a64-1e1a1c2b3d4e", "CR-1")]
    [InlineData("scan::8f0e7f62-4bb9-4c34-9a64-1e1a1c2b3d4e", "cr-101")]
    public void Wrong_scan_or_courier_ids_are_rejected(string scanId, string courierId)
    {
        var upload = TestData.Scan(ScanType.Pickup, scanId: scanId, courierId: courierId);

        Assert.Throws<DomainException>(() => TrackingScan.Record(TestData.Shipment(), upload.Details, TestData.Now));
    }

    [Fact]
    public void An_unknown_scan_type_and_a_blank_location_are_rejected()
    {
        var shipment = TestData.Shipment();
        var unknownType = TestData.Scan((ScanType)42).Details;
        var blankLocation = TestData.Scan(ScanType.Pickup).Details with { Location = " " };

        Assert.Throws<DomainException>(() => TrackingScan.Record(shipment, unknownType, TestData.Now));
        Assert.Throws<DomainException>(() => TrackingScan.Record(shipment, blankLocation, TestData.Now));
    }

    [Fact]
    public void A_scan_time_that_is_not_utc_is_rejected()
    {
        var local = TestData.Scan(ScanType.Pickup).Details with
        {
            ScannedAtUtc = DateTime.SpecifyKind(TestData.Now, DateTimeKind.Unspecified),
        };

        Assert.Throws<DomainException>(() => TrackingScan.Record(TestData.Shipment(), local, TestData.Now));
    }
}
