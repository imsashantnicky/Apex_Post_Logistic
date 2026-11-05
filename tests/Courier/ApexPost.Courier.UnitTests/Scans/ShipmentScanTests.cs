using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.UnitTests.Scans;

public sealed class ShipmentScanTests
{
    private const string ValidTrackingNumber = "APX-10000001";
    private const string ValidCourierId = "CR-101";
    private const string Location = "BR001";

    [Theory]
    [InlineData("APX-10000001", "APX-10000001")]
    [InlineData(" apx-12345678 ", "APX-12345678")]
    public void Valid_tracking_number_is_trimmed_and_upper_case(string input, string expected)
    {
        var scan = Create(input, ValidCourierId);

        Assert.Equal(expected, scan.TrackingNumber);
    }

    [Theory]
    [InlineData("APX-1234567")]
    [InlineData("APX-123456789")]
    [InlineData("APX12345678")]
    [InlineData("ABC-12345678")]
    [InlineData("APX-1234567A")]
    [InlineData("APX-١٢٣٤٥٦٧٨")]
    public void Tracking_number_outside_the_format_is_rejected(string input)
    {
        var error = Assert.Throws<ArgumentException>(() => Create(input, ValidCourierId));

        Assert.Equal("trackingNumber", error.ParamName);
    }

    [Theory]
    [InlineData("CR-101")]
    [InlineData(" CR-007 ")]
    public void Valid_courier_id_is_accepted(string input)
    {
        var scan = Create(ValidTrackingNumber, input);

        Assert.Equal(input.Trim(), scan.CourierId);
    }

    [Theory]
    [InlineData("CR-10")]
    [InlineData("CR-1001")]
    [InlineData("cr-101")]
    [InlineData("CR101")]
    [InlineData("XX-101")]
    public void Courier_id_outside_the_format_is_rejected(string input)
    {
        var error = Assert.Throws<ArgumentException>(() => Create(ValidTrackingNumber, input));

        Assert.Equal("courierId", error.ParamName);
    }

    [Fact]
    public void Restore_applies_the_same_rules()
    {
        Assert.Throws<ArgumentException>(() => ShipmentScan.Restore(
            ScanIds.New(), "APX-1", ScanType.Pickup, ValidCourierId, Location,
            DateTimeOffset.UnixEpoch, null, SyncStatus.Pending));
    }

    [Fact]
    public void Scan_rules_publish_the_shared_patterns()
    {
        Assert.Equal(@"^APX-\d{8}$", ScanRules.TrackingNumberPattern);
        Assert.Equal(@"^CR-\d{3}$", ScanRules.CourierIdPattern);
        Assert.False(ScanRules.IsValidTrackingNumber(null));
        Assert.False(ScanRules.IsValidCourierId(null));
    }

    private static ShipmentScan Create(string trackingNumber, string courierId) =>
        ShipmentScan.CreateNew(
            trackingNumber, ScanType.Pickup, courierId, Location, null, TimeProvider.System);
}
