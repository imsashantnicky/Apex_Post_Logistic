using System.Globalization;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.Presentation.ViewModels;

/// <summary>One scan as a list row.</summary>
/// <param name="Id">Scan document id (<c>scan::{Guid}</c>).</param>
/// <param name="TrackingNumber">Tracking number.</param>
/// <param name="ScanType">Kind of scan.</param>
/// <param name="Location">Where it was scanned.</param>
/// <param name="ScannedAtText">Local scan time, formatted for the current culture.</param>
/// <param name="SyncStatus">Upload state.</param>
public sealed record ScanRow(
    string Id,
    string TrackingNumber,
    ScanType ScanType,
    string Location,
    string ScannedAtText,
    SyncStatus SyncStatus)
{
    /// <summary>Second line of the row: "Pickup · Central Hub · 07/11/2026 10:15".</summary>
    public string Details => $"{ScanType} · {Location} · {ScannedAtText}";

    /// <summary>Builds the row of a stored scan.</summary>
    /// <param name="scan">The scan.</param>
    public static ScanRow From(ShipmentScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        return new ScanRow(
            scan.Id,
            scan.TrackingNumber,
            scan.ScanType,
            scan.Location,
            scan.ScannedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            scan.SyncStatus);
    }
}
