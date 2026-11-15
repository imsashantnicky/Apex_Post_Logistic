using ApexPost.Courier.Domain.Scans;
using ApexPost.Domain.Tracking;

namespace ApexPost.Tracking.UnitTests.Domain;

/// <summary>The server copies the device formats; these tests keep the two copies identical.</summary>
[Trait("Category", "Unit")]
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
    [InlineData("APX-00000000", true)]
    [InlineData("apx-10000001", false)]
    [InlineData("APX-1000000", false)]
    [InlineData("APX-100000011", false)]
    [InlineData("APX10000001", false)]
    [InlineData(" APX-10000001", false)]
    [InlineData("APX-10000001 ", false)]
    [InlineData("APX-1000 0001", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTrackingNumber_Value_MatchesTheFormatAndTheDeviceRule(string? value, bool expected)
    {
        // Arrange
        var device = ScanRules.IsValidTrackingNumber(value);

        // Act
        var server = TrackingFormats.IsTrackingNumber(value);

        // Assert
        Assert.Equal(expected, server);
        Assert.Equal(device, server);
    }

    [Theory]
    [InlineData("CR-101", true)]
    [InlineData("CR-000", true)]
    [InlineData("CR-099", true)]
    [InlineData("CR-999", true)]
    [InlineData("CR-99", false)]
    [InlineData("CR-10", false)]
    [InlineData("CR-1000", false)]
    [InlineData("CR-1010", false)]
    [InlineData("cr-101", false)]
    [InlineData("CR101", false)]
    [InlineData(" CR-101", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCourierId_Value_MatchesTheFormatAndTheDeviceRule(string? value, bool expected)
    {
        // Arrange
        var device = ScanRules.IsValidCourierId(value);

        // Act
        var server = TrackingFormats.IsCourierId(value);

        // Assert
        Assert.Equal(expected, server);
        Assert.Equal(device, server);
    }
}
