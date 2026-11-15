using ApexPost.Api.Contracts;
using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Tests.Support;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Tests.Contracts;

/// <summary>The rules of one uploaded scan, including both custom rules.</summary>
public sealed class ScanItemTests
{
    private static readonly DateTime Now = FixedTimeProvider.DefaultNow.UtcDateTime;

    [Theory]
    [InlineData(ScanType.Pickup)]
    [InlineData(ScanType.InTransit)]
    [InlineData(ScanType.Delivered)]
    [InlineData(ScanType.Failed)]
    public void A_complete_scan_is_valid(ScanType type) =>
        Assert.Empty(ContractValidator.Validate(Requests.Scan(type)));

    [Theory]
    [InlineData("scan::0f8fad5b-d9cb-469f-a165-70867728950e", true)]
    [InlineData("scan::0F8FAD5B-D9CB-469F-A165-70867728950E", true)]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e", false)]
    [InlineData("scan:0f8fad5b-d9cb-469f-a165-70867728950e", false)]
    [InlineData("scan::0f8fad5b-d9cb-469f-a165-70867728950", false)]
    [InlineData("scan::0f8fad5b-d9cb-469f-a165-70867728950ee", false)]
    [InlineData("scan::zf8fad5b-d9cb-469f-a165-70867728950e", false)]
    [InlineData("", false)]
    public void Scan_id_is_scan_and_a_36_character_guid(string scanId, bool valid)
    {
        var scan = Copy(Requests.Scan(), scanId: scanId);

        Assert.Equal(valid, !ContractValidator.FailedMembers(scan).Contains(nameof(scan.ScanId)));
    }

    [Theory]
    [InlineData("CR-101", true)]
    [InlineData("CR-10", false)]
    [InlineData("CR-1010", false)]
    [InlineData("cr-101", false)]
    [InlineData("CR101", false)]
    [InlineData("", false)]
    public void Courier_id_is_CR_and_3_digits(string courierId, bool valid)
    {
        var scan = Copy(Requests.Scan(), courierId: courierId);

        var results = ContractValidator.Validate(scan);

        Assert.Equal(valid, !results.Any(result => result.MemberNames.Contains(nameof(scan.CourierId))));
        if (!valid && courierId.Length > 0)
        {
            Assert.Contains(results, result => result.ErrorMessage == ContractRules.CourierIdMessage);
        }
    }

    [Theory]
    [InlineData("APX-20000001", true)]
    [InlineData("APX-2000000", false)]
    [InlineData("XPA-20000001", false)]
    public void Scan_tracking_number_must_be_APX_and_8_digits(string trackingNumber, bool valid)
    {
        var scan = Copy(Requests.Scan(), trackingNumber: trackingNumber);

        Assert.Equal(valid, !ContractValidator.FailedMembers(scan).Contains(nameof(scan.TrackingNumber)));
    }

    [Theory]
    [InlineData(-60 * 24, true)]
    [InlineData(0, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    [InlineData(60 * 60, false)]
    public void Scanned_at_may_be_at_most_two_minutes_ahead_of_the_server(int secondsFromNow, bool valid)
    {
        var scan = Copy(Requests.Scan(), scannedAtUtc: Now.AddSeconds(secondsFromNow));

        Assert.Equal(valid, !ContractValidator.FailedMembers(scan).Contains(nameof(scan.ScannedAtUtc)));
    }

    [Fact]
    public void Scanned_at_is_required()
    {
        var scan = Copy(Requests.Scan(), clearTime: true);

        Assert.Contains(nameof(scan.ScannedAtUtc), ContractValidator.FailedMembers(scan));
    }

    [Fact]
    public void Scan_type_is_required_and_must_be_defined()
    {
        Assert.Contains(
            nameof(ScanItem.ScanType),
            ContractValidator.FailedMembers(Copy(Requests.Scan(), clearType: true)));
        Assert.Contains(
            nameof(ScanItem.ScanType),
            ContractValidator.FailedMembers(Copy(Requests.Scan(), type: (ScanType)9)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_Delivered_scan_needs_the_name_of_who_signed(string? signature)
    {
        var scan = Copy(Requests.Scan(ScanType.Delivered), signature: signature, clearSignature: true);

        var results = ContractValidator.Validate(scan);

        var result = Assert.Single(results);
        Assert.Equal(ContractRules.SignatureMessage, result.ErrorMessage);
        Assert.Equal([nameof(ScanItem.RecipientSignatureName)], result.MemberNames);
    }

    [Theory]
    [InlineData(ScanType.Pickup)]
    [InlineData(ScanType.InTransit)]
    [InlineData(ScanType.Failed)]
    public void Other_scans_need_no_signature(ScanType type)
    {
        var scan = Copy(Requests.Scan(type), clearSignature: true);

        Assert.Empty(ContractValidator.Validate(scan));
    }

    [Fact]
    public void Location_is_required_and_limited_in_length()
    {
        Assert.Contains(
            nameof(ScanItem.Location),
            ContractValidator.FailedMembers(Copy(Requests.Scan(), location: "")));
        Assert.Contains(
            nameof(ScanItem.Location),
            ContractValidator.FailedMembers(
                Copy(Requests.Scan(), location: new string('x', TrackingScan.LocationLength + 1))));
    }

    [Fact]
    public void Notes_are_optional_but_limited_in_length()
    {
        Assert.Empty(ContractValidator.Validate(Copy(Requests.Scan(), notes: null, clearNotes: true)));
        Assert.Contains(
            nameof(ScanItem.Notes),
            ContractValidator.FailedMembers(
                Copy(Requests.Scan(), notes: new string('x', TrackingScan.NotesLength + 1))));
    }

    private static ScanItem Copy(
        ScanItem source,
        string? scanId = null,
        string? trackingNumber = null,
        string? courierId = null,
        string? location = null,
        DateTime? scannedAtUtc = null,
        ScanType? type = null,
        string? notes = null,
        string? signature = null,
        bool clearTime = false,
        bool clearType = false,
        bool clearNotes = false,
        bool clearSignature = false) => new()
    {
        ScanId = scanId ?? source.ScanId,
        TrackingNumber = trackingNumber ?? source.TrackingNumber,
        ScanType = clearType ? null : type ?? source.ScanType,
        CourierId = courierId ?? source.CourierId,
        Location = location ?? source.Location,
        ScannedAtUtc = clearTime ? null : scannedAtUtc ?? source.ScannedAtUtc,
        Notes = clearNotes ? null : notes ?? source.Notes,
        RecipientSignatureName = clearSignature ? signature : source.RecipientSignatureName,
    };
}
