using System.Globalization;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using Couchbase.Lite;

namespace ApexPost.Courier.Offline.Scans;

/// <summary>
/// Converts between <see cref="ShipmentScan"/> and its JSON document. Enums are stored as strings and
/// <c>scannedAtUtc</c> as an ISO-8601 UTC string (<c>"O"</c> format), so ordering by it is chronological.
/// </summary>
internal static class ScanDocumentMapper
{
    internal const string TypeField = "type";
    internal const string TypeValue = "shipmentScan";
    internal const string TrackingNumberField = "trackingNumber";
    internal const string ScanTypeField = "scanType";
    internal const string CourierIdField = "courierId";
    internal const string LocationField = "location";
    internal const string ScannedAtUtcField = "scannedAtUtc";
    internal const string NotesField = "notes";
    internal const string SyncStatusField = "syncStatus";

    private const string RoundTripFormat = "O";

    /// <summary>Creates a new document whose id is the scan id.</summary>
    internal static MutableDocument ToDocument(ShipmentScan scan) => Write(new MutableDocument(scan.Id), scan);

    /// <summary>Copies every scan field onto <paramref name="document"/> and returns it.</summary>
    internal static MutableDocument Write(MutableDocument document, ShipmentScan scan)
    {
        document.SetString(TypeField, TypeValue);
        document.SetString(TrackingNumberField, scan.TrackingNumber);
        document.SetString(ScanTypeField, scan.ScanType.ToString());
        document.SetString(CourierIdField, scan.CourierId);
        document.SetString(LocationField, scan.Location);
        document.SetString(ScannedAtUtcField, FormatTimestamp(scan.ScannedAtUtc));
        document.SetString(NotesField, scan.Notes);
        document.SetString(SyncStatusField, scan.SyncStatus.ToString());
        return document;
    }

    /// <summary>Rebuilds a scan from a stored document.</summary>
    /// <exception cref="FormatException">A field is missing or has an invalid value.</exception>
    internal static ShipmentScan FromDocument(Document document) => FromDictionary(document.Id, document);

    /// <summary>Rebuilds a scan from a document body, for example a row returned by a query.</summary>
    /// <exception cref="FormatException">A field is missing or has an invalid value.</exception>
    internal static ShipmentScan FromDictionary(string id, IDictionaryObject values)
    {
        try
        {
            return ShipmentScan.Restore(
                id,
                Required(values, TrackingNumberField),
                ParseEnum<ScanType>(values, ScanTypeField),
                Required(values, CourierIdField),
                Required(values, LocationField),
                DateTimeOffset.ParseExact(
                    Required(values, ScannedAtUtcField), RoundTripFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                values.GetString(NotesField),
                ParseEnum<SyncStatus>(values, SyncStatusField));
        }
        catch (ArgumentException ex)
        {
            throw new FormatException($"Document '{id}' is not a valid scan: {ex.Message}", ex);
        }
    }

    /// <summary>Formats a time as a UTC round-trip string ending in <c>Z</c>.</summary>
    internal static string FormatTimestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString(RoundTripFormat, CultureInfo.InvariantCulture);

    private static string Required(IDictionaryObject values, string field) =>
        values.GetString(field) ?? throw new FormatException($"The field '{field}' is missing.");

    // Only the enum names are accepted; Enum.TryParse alone would also accept numbers such as "2".
    internal static T ParseEnum<T>(IDictionaryObject values, string field)
        where T : struct, Enum
    {
        var text = Required(values, field);
        return Enum.GetNames<T>().Contains(text, StringComparer.Ordinal)
            ? Enum.Parse<T>(text)
            : throw new FormatException($"The field '{field}' has an unknown {typeof(T).Name} value.");
    }
}
