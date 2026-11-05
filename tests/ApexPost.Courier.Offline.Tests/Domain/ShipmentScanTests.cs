using System.Text.RegularExpressions;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Tests.Support;

namespace ApexPost.Courier.Offline.Tests.Domain;

public sealed partial class ShipmentScanTests
{
    private static readonly FixedTimeProvider Clock = new(TestScans.Start);

    private static ShipmentScan NewScan(string trackingNumber = "APX-10000001", string courierId = "CR-101") =>
        ShipmentScan.CreateNew(trackingNumber, ScanType.Pickup, courierId, "North Hub", null, Clock);

    [Fact]
    public void A_new_scan_id_is_scan_prefix_and_a_guid()
    {
        var scan = NewScan();

        Assert.Matches(ScanIdRegex(), scan.Id);
        Assert.True(Guid.TryParse(scan.Id["scan::".Length..], out _));
    }

    [Fact]
    public void Two_new_scans_get_different_ids()
    {
        Assert.NotEqual(NewScan().Id, NewScan().Id);
    }

    [Fact]
    public void A_new_scan_starts_pending()
    {
        Assert.Equal(SyncStatus.Pending, NewScan().SyncStatus);
    }

    [Theory]
    [InlineData("APX-1234567")]
    [InlineData("APX-123456789")]
    [InlineData("APX12345678")]
    [InlineData("ABC-12345678")]
    [InlineData("APX-1234567A")]
    [InlineData("APX-12345678' OR '1'='1")]
    public void A_tracking_number_in_the_wrong_format_is_rejected(string trackingNumber)
    {
        Assert.Throws<ArgumentException>(() => NewScan(trackingNumber: trackingNumber));
    }

    [Theory]
    [InlineData("CR-12")]
    [InlineData("CR-1234")]
    [InlineData("CR101")]
    [InlineData("XX-101")]
    [InlineData("cr-101")]
    public void A_courier_id_in_the_wrong_format_is_rejected(string courierId)
    {
        Assert.Throws<ArgumentException>(() => NewScan(courierId: courierId));
    }

    [Fact]
    public void A_lower_case_tracking_number_is_stored_upper_case()
    {
        Assert.Equal("APX-10000001", NewScan(trackingNumber: "apx-10000001").TrackingNumber);
    }

    [GeneratedRegex(@"^scan::[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    private static partial Regex ScanIdRegex();
}
