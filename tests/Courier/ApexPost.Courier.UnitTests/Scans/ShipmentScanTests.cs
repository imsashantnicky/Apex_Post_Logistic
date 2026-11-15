using System.Text.RegularExpressions;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Courier.UnitTests.Scans;

/// <summary>
/// The scan document rules of the courier domain. Week 5 step 02 merged the duplicate class from
/// ApexPost.Courier.Offline.Tests into this one (see TESTING-STRATEGY.md section 5).
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class ShipmentScanTests
{
    private const string ValidTrackingNumber = "APX-10000001";
    private const string ValidCourierId = "CR-101";
    private const string Location = "BR001";

    private static readonly DateTimeOffset ScannedAt = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("APX-10000001", "APX-10000001")]
    [InlineData(" apx-12345678 ", "APX-12345678")]
    public void CreateNew_ValidTrackingNumber_IsTrimmedAndUpperCase(string input, string expected)
    {
        // Arrange
        var trackingNumber = input;

        // Act
        var scan = Create(trackingNumber, ValidCourierId);

        // Assert
        Assert.Equal(expected, scan.TrackingNumber);
    }

    [Theory]
    [InlineData("APX-1234567")]
    [InlineData("APX-123456789")]
    [InlineData("APX12345678")]
    [InlineData("ABC-12345678")]
    [InlineData("APX-1234567A")]
    [InlineData("APX-١٢٣٤٥٦٧٨")]
    [InlineData("APX-12345678' OR '1'='1")]
    public void CreateNew_TrackingNumberOutsideTheFormat_ThrowsArgumentException(string input)
    {
        // Arrange
        var create = () => Create(input, ValidCourierId);

        // Act
        var error = Assert.Throws<ArgumentException>(create);

        // Assert
        Assert.Equal("trackingNumber", error.ParamName);
    }

    [Theory]
    [InlineData("CR-101")]
    [InlineData(" CR-007 ")]
    public void CreateNew_ValidCourierId_IsAcceptedTrimmed(string input)
    {
        // Arrange
        var courierId = input;

        // Act
        var scan = Create(ValidTrackingNumber, courierId);

        // Assert
        Assert.Equal(input.Trim(), scan.CourierId);
    }

    [Theory]
    [InlineData("CR-10")]
    [InlineData("CR-1001")]
    [InlineData("cr-101")]
    [InlineData("CR101")]
    [InlineData("XX-101")]
    public void CreateNew_CourierIdOutsideTheFormat_ThrowsArgumentException(string input)
    {
        // Arrange
        var create = () => Create(ValidTrackingNumber, input);

        // Act
        var error = Assert.Throws<ArgumentException>(create);

        // Assert
        Assert.Equal("courierId", error.ParamName);
    }

    [Fact]
    public void CreateNew_AnyValidScan_GetsScanPrefixAndGuidId()
    {
        // Arrange
        var trackingNumber = ValidTrackingNumber;

        // Act
        var scan = Create(trackingNumber, ValidCourierId);

        // Assert
        Assert.Matches(ScanIdRegex(), scan.Id);
        Assert.True(Guid.TryParse(scan.Id["scan::".Length..], out _));
    }

    [Fact]
    public void CreateNew_TwoScans_GetDifferentIds()
    {
        // Arrange
        var first = Create(ValidTrackingNumber, ValidCourierId);

        // Act
        var second = Create(ValidTrackingNumber, ValidCourierId);

        // Assert
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void CreateNew_AnyValidScan_StartsPendingAtTheClockTime()
    {
        // Arrange
        var trackingNumber = ValidTrackingNumber;

        // Act
        var scan = Create(trackingNumber, ValidCourierId);

        // Assert
        Assert.Equal(SyncStatus.Pending, scan.SyncStatus);
        Assert.Equal(ScannedAt, scan.ScannedAtUtc);
    }

    [Fact]
    public void Restore_TrackingNumberOutsideTheFormat_ThrowsArgumentException()
    {
        // Arrange
        var restore = () => ShipmentScan.Restore(
            ScanIds.New(), "APX-1", ScanType.Pickup, ValidCourierId, Location,
            DateTimeOffset.UnixEpoch, null, SyncStatus.Pending);

        // Act
        var error = Assert.Throws<ArgumentException>(restore);

        // Assert
        Assert.Equal("trackingNumber", error.ParamName);
    }

    [Fact]
    public void ScanRules_SharedPatterns_MatchTheContractAndRejectNull()
    {
        // Arrange
        string? missing = null;

        // Act
        var trackingNumberValid = ScanRules.IsValidTrackingNumber(missing);
        var courierIdValid = ScanRules.IsValidCourierId(missing);

        // Assert
        Assert.Equal(@"^APX-\d{8}$", ScanRules.TrackingNumberPattern);
        Assert.Equal(@"^CR-\d{3}$", ScanRules.CourierIdPattern);
        Assert.False(trackingNumberValid);
        Assert.False(courierIdValid);
    }

    private static ShipmentScan Create(string trackingNumber, string courierId) =>
        ShipmentScan.CreateNew(
            trackingNumber, ScanType.Pickup, courierId, Location, null, new FakeTimeProvider(ScannedAt));

    [GeneratedRegex(@"^scan::[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    private static partial Regex ScanIdRegex();
}
