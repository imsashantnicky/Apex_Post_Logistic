using ApexPost.Application.Tracking;

namespace ApexPost.Api.Contracts.V2;

/// <summary>Maps a shipment view to the reduced partner response.</summary>
public static class PartnerShipmentMapping
{
    /// <summary>Status, destination city and latest scan only; no recipient details.</summary>
    public static PartnerShipmentResponse ToPartnerResponse(this ShipmentView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var scan = view.LatestScan;
        return new PartnerShipmentResponse(
            view.TrackingNumber,
            view.Status,
            view.RecipientCity,
            scan is null ? null : new LatestScanResponse(scan.ScanType, scan.Location, scan.ScannedAtUtc));
    }
}
