using ApexPost.Courier.Domain.Scans;
using ApexPost.Domain.Tracking;

namespace ApexPost.Tracking.UnitTests.Domain;

/// <summary>The server copies the device formats; these tests keep the two copies identical.</summary>
public sealed class TrackingFormatsTests
{
    [Fact]
    public void The_patterns_equal_the_courier_device_rules()
    {
        Assert.Equal(ScanRules.TrackingNumberPattern, TrackingFormats.TrackingNumberPattern);
        Assert.Equal(ScanRules.CourierIdPattern, TrackingFormats.CourierIdPattern);
        Assert.Equal(ScanIds.Prefix, TrackingFormats.ScanIdPrefix);
    }

    [Fact]
    public void A_device_scan_id_is_a_valid_server_scan_id_of_the_stored_length()
    {
        var scanId = ScanIds.New();

        Assert.True(TrackingFormats.IsScanId(scanId));
        Assert.Equal(TrackingFormats.ScanIdLength, scanId.Length);
    }

    [Theory]
    [InlineData("APX-10000001", true)]
    [InlineData("APX-99999999", true)]
    [InlineData("APX-1000000", false)]
    [InlineData("APX-100000011", false)]
    [InlineData(" APX-10000001", false)]
    [InlineData(null, false)]
    public void Tracking_numbers_are_judged_like_on_the_device(string? value, bool expected)
    {
        Assert.Equal(expected, TrackingFormats.IsTrackingNumber(value));
        Assert.Equal(ScanRules.IsValidTrackingNumber(value), TrackingFormats.IsTrackingNumber(value));
    }

    [Theory]
    [InlineData("CR-101", true)]
    [InlineData("CR-10", false)]
    [InlineData("CR-1010", false)]
    [InlineData(null, false)]
    public void Courier_ids_are_judged_like_on_the_device(string? value, bool expected)
    {
        Assert.Equal(expected, TrackingFormats.IsCourierId(value));
        Assert.Equal(ScanRules.IsValidCourierId(value), TrackingFormats.IsCourierId(value));
    }
}
