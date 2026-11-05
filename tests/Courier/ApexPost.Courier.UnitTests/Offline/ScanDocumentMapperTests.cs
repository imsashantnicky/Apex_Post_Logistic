using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Scans;
using Couchbase.Lite;

namespace ApexPost.Courier.UnitTests.Offline;

public sealed class ScanDocumentMapperTests
{
    private static readonly DateTimeOffset ScannedAt = new(2026, 11, 5, 9, 30, 0, TimeSpan.FromHours(2));

    private static ShipmentScan Scan(string? notes = "Left at reception") =>
        ShipmentScan.Restore(
            ScanIds.New(), "APX-10000001", ScanType.Pickup, "CR-101", "BR001",
            ScannedAt, notes, SyncStatus.Synced);

    [Fact]
    public void ToDocument_writes_the_exact_field_names_with_enums_as_strings()
    {
        var scan = Scan();

        using var document = ScanDocumentMapper.ToDocument(scan);

        Assert.Equal(scan.Id, document.Id);
        Assert.Equal(
            ["courierId", "location", "notes", "scanType", "scannedAtUtc", "syncStatus", "trackingNumber", "type"],
            document.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("shipmentScan", document.GetString("type"));
        Assert.Equal("APX-10000001", document.GetString("trackingNumber"));
        Assert.Equal("Pickup", document.GetString("scanType"));
        Assert.Equal("CR-101", document.GetString("courierId"));
        Assert.Equal("BR001", document.GetString("location"));
        Assert.Equal("Left at reception", document.GetString("notes"));
        Assert.Equal("Synced", document.GetString("syncStatus"));
    }

    [Fact]
    public void ScannedAtUtc_is_stored_as_round_trip_utc_string()
    {
        using var document = ScanDocumentMapper.ToDocument(Scan());

        Assert.Equal("2026-11-05T07:30:00.0000000Z", document.GetString("scannedAtUtc"));
    }

    [Fact]
    public void Timestamps_sort_as_strings_in_time_order()
    {
        var earlier = ScanDocumentMapper.FormatTimestamp(new DateTimeOffset(2026, 1, 9, 23, 0, 0, TimeSpan.Zero));
        var later = ScanDocumentMapper.FormatTimestamp(new DateTimeOffset(2026, 1, 10, 1, 0, 0, TimeSpan.Zero));

        Assert.True(string.CompareOrdinal(earlier, later) < 0);
    }

    [Fact]
    public void FromDictionary_restores_every_field()
    {
        var scan = Scan();
        using var document = ScanDocumentMapper.ToDocument(scan);

        var restored = ScanDocumentMapper.FromDictionary(scan.Id, document);

        Assert.Equal(scan.Id, restored.Id);
        Assert.Equal(scan.TrackingNumber, restored.TrackingNumber);
        Assert.Equal(scan.ScanType, restored.ScanType);
        Assert.Equal(scan.CourierId, restored.CourierId);
        Assert.Equal(scan.Location, restored.Location);
        Assert.Equal(scan.ScannedAtUtc, restored.ScannedAtUtc);
        Assert.Equal(scan.Notes, restored.Notes);
        Assert.Equal(scan.SyncStatus, restored.SyncStatus);
    }

    [Fact]
    public void Missing_notes_round_trip_as_null()
    {
        var scan = Scan(notes: null);
        using var document = ScanDocumentMapper.ToDocument(scan);

        Assert.Null(ScanDocumentMapper.FromDictionary(scan.Id, document).Notes);
    }

    [Theory]
    [InlineData("scanType", "Lost")]
    [InlineData("syncStatus", "2")]
    [InlineData("trackingNumber", "APX-1")]
    [InlineData("scannedAtUtc", "yesterday")]
    public void Invalid_stored_value_throws_FormatException(string field, string value)
    {
        var scan = Scan();
        using var document = ScanDocumentMapper.ToDocument(scan);
        document.SetString(field, value);

        Assert.Throws<FormatException>(() => ScanDocumentMapper.FromDictionary(scan.Id, document));
    }

    [Fact]
    public void Missing_required_field_throws_FormatException()
    {
        var scan = Scan();
        using var document = ScanDocumentMapper.ToDocument(scan);
        document.Remove("courierId");

        Assert.Throws<FormatException>(() => ScanDocumentMapper.FromDictionary(scan.Id, document));
    }

    [Fact]
    public void Document_implements_the_dictionary_used_by_FromDictionary()
    {
        Assert.True(typeof(IDictionaryObject).IsAssignableFrom(typeof(Document)));
    }
}
