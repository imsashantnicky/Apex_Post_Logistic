using System.Text.Json.Serialization;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V2;

/// <summary>A shipment in v2: a nested recipient, the latest scan and the status by name.</summary>
/// <param name="TrackingNumber">The tracking number.</param>
/// <param name="Recipient">Who receives the shipment.</param>
/// <param name="WeightKg">The weight in kilograms.</param>
/// <param name="Status">The current status.</param>
/// <param name="LatestScan">The most recent scan, or null.</param>
/// <param name="CreatedAtUtc">When the shipment was registered.</param>
/// <param name="Version">The change counter.</param>
public sealed record ShipmentResponse(
    string TrackingNumber,
    RecipientResponse Recipient,
    decimal WeightKg,
    ShipmentStatus Status,
    LatestScanResponse? LatestScan,
    DateTime CreatedAtUtc,
    int Version)
{
    /// <summary>The tracking number, APX- followed by 8 digits.</summary>
    /// <example>APX-20000001</example>
    public string TrackingNumber { get; init; } = TrackingNumber;

    /// <summary>Who receives the shipment and where.</summary>
    public RecipientResponse Recipient { get; init; } = Recipient;

    /// <summary>The weight in kilograms.</summary>
    /// <example>1.2</example>
    public decimal WeightKg { get; init; } = WeightKg;

    /// <summary>The current status: Created, PickedUp, InTransit, OutForDelivery, Delivered or Failed.</summary>
    /// <example>InTransit</example>
    [JsonConverter(typeof(JsonStringEnumConverter<ShipmentStatus>))]
    public ShipmentStatus Status { get; init; } = Status;

    /// <summary>The most recent scan by scannedAtUtc; null when no scan was uploaded yet.</summary>
    public LatestScanResponse? LatestScan { get; init; } = LatestScan;

    /// <summary>When the shipment was registered, in UTC.</summary>
    /// <example>2026-11-05T08:00:00.0000000Z</example>
    public DateTime CreatedAtUtc { get; init; } = CreatedAtUtc;

    /// <summary>A counter that grows with every change of the shipment.</summary>
    /// <example>3</example>
    public int Version { get; init; } = Version;
}
