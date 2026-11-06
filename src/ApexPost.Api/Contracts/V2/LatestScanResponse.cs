using System.Text.Json.Serialization;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V2;

/// <summary>The most recent scan of a shipment, in short.</summary>
/// <param name="ScanType">What the courier recorded.</param>
/// <param name="Location">Where the scan happened.</param>
/// <param name="ScannedAtUtc">When the scan happened.</param>
public sealed record LatestScanResponse(ScanType ScanType, string Location, DateTime ScannedAtUtc)
{
    /// <summary>What the courier recorded: Pickup, InTransit, Delivered or Failed.</summary>
    /// <example>InTransit</example>
    [JsonConverter(typeof(JsonStringEnumConverter<ScanType>))]
    public ScanType ScanType { get; init; } = ScanType;

    /// <summary>Where the scan happened.</summary>
    /// <example>Central Hub</example>
    public string Location { get; init; } = Location;

    /// <summary>When the scan happened, in UTC.</summary>
    /// <example>2026-11-05T09:30:00.0000000Z</example>
    public DateTime ScannedAtUtc { get; init; } = ScannedAtUtc;
}
