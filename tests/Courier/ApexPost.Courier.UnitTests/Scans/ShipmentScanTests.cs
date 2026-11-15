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
    [InlineData("APX-1234 5678")]
    public void CreateNew_TrackingNumberOutsideTheFormat_ThrowsArgumentExceptionWithThePattern(string input)
    {
        // Arrange
        var create = () => Create(input, ValidCourierId);

        // Act
        var error = Assert.Throws<ArgumentException>(create);

        // Assert
        Assert.Equal("trackingNumber", error.ParamName);
        Assert.Equal(
            $"'{input.Trim().ToUpperInvariant()}' does not match the format {ScanRules.TrackingNumberPattern}. "
            + "(Parameter 'trackingNumber')",
            error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateNew_BlankTrackingNumber_ThrowsArgumentException(string input)
    {
        // Arrange
        var create = () => Create(input, ValidCourierId);

        // Act
        var error = Assert.Throws<ArgumentException>(create);

        // Assert
        Assert.Equal("trackingNumber", error.ParamName);
    }

    [Fact]
    public void CreateNew_NullTrackingNumber_ThrowsArgumentNullException()
    {
        // Arrange
        var create = () => Create(null!, ValidCourierId);

        // Act
        var error = Assert.Throws<ArgumentNullException>(create);

        // Assert
        Assert.Equal("trackingNumber", error.ParamName);
    }

    [Theory]
    [InlineData("CR-101")]
    [InlineData(" CR-007 ")]
    [InlineData("CR-000")]
    [InlineData("CR-099")]
    [InlineData("CR-999")]
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
    [InlineData("CR-99")]
    [InlineData("CR-1000")]
    public void CreateNew_CourierIdOutsideTheFormat_ThrowsArgumentExceptionWithThePattern(string input)
    {
        // Arrange
        var create = () => Create(ValidTrackingNumber, input);

        // Act
        var error = Assert.Throws<ArgumentException>(create);

        // Assert
        Assert.Equal("courierId", error.ParamName);
        Assert.Equal(
            $"'{input}' does not match the format {ScanRules.CourierIdPattern}. (Parameter 'courierId')",
            error.Message);
    }

    [Fact]
    public void CreateNew_UndefinedScanType_ThrowsArgumentExceptionForScanType()
    {
        // Arrange
        var create = () => ShipmentScan.CreateNew(
            ValidTrackingNumber, (ScanType)99, ValidCourierId, Location, null, new FakeTimeProvider(ScannedAt));

        // Act
        var error = Assert.Throws<ArgumentException>(create);

        // Assert
        Assert.Equal("scanType", error.ParamName);
        Assert.Equal("Unknown ScanType value 99. (Parameter 'scanType')", error.Message);
    }

    [Theory]
    [InlineData("not-a-scan-id")]
    [InlineData("scan::not-a-guid")]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("")]
    public void Restore_IdNotScanPrefixAndGuid_ThrowsArgumentExceptionForId(string id)
    {
        // Arrange
        var restore = () => ShipmentScan.Restore(
            id, ValidTrackingNumber, ScanType.Pickup, ValidCourierId, Location,
            ScannedAt, null, SyncStatus.Pending);

        // Act
        var error = Assert.Throws<ArgumentException>(restore);

        // Assert
        Assert.Equal("id", error.ParamName);
        Assert.Equal("Scan id must look like 'scan::{guid}'. (Parameter 'id')", error.Message);
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
