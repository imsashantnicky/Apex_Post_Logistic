using ApexPost.Domain.Tracking;

namespace ApexPost.Application.Tracking;

/// <summary>A tracked shipment as callers see it, with its most recent scan if there is one.</summary>
public sealed record ShipmentView(
    string TrackingNumber,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string RecipientCity,
    decimal WeightKg,
    ShipmentStatus Status,
    DateTime CreatedAtUtc,
    int Version,
    ScanView? LatestScan);
