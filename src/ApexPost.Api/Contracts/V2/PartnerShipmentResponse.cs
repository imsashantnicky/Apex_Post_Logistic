using System.Text.Json.Serialization;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V2;

/// <summary>
/// What a partner sees of a shipment: status, destination city and the latest scan. The name, phone and address of
/// the recipient are left out, because a partner only needs to track the parcel.
/// </summary>
/// <param name="TrackingNumber">The tracking number.</param>
/// <param name="Status">The current status.</param>
/// <param name="DestinationCity">The city the shipment goes to.</param>
/// <param name="LatestScan">The most recent scan, or null.</param>
public sealed record PartnerShipmentResponse(
    string TrackingNumber,
    ShipmentStatus Status,
    string DestinationCity,
    LatestScanResponse? LatestScan)
{
    /// <summary>The tracking number, APX- followed by 8 digits.</summary>
    /// <example>APX-20000005</example>
    public string TrackingNumber { get; init; } = TrackingNumber;

    /// <summary>The current status: Created, PickedUp, InTransit, OutForDelivery, Delivered or Failed.</summary>
    /// <example>InTransit</example>
    [JsonConverter(typeof(JsonStringEnumConverter<ShipmentStatus>))]
    public ShipmentStatus Status { get; init; } = Status;

    /// <summary>The city of the recipient.</summary>
    /// <example>Riverton</example>
    public string DestinationCity { get; init; } = DestinationCity;

    /// <summary>The most recent scan by scannedAtUtc; null when no scan was uploaded yet.</summary>
    public LatestScanResponse? LatestScan { get; init; } = LatestScan;
}
