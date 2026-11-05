using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>A shipment in v1: flat recipient fields and the status as a number.</summary>
public sealed record ShipmentResponse(
    string TrackingNumber,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string RecipientCity,
    decimal WeightKg,
    ShipmentStatus Status,
    DateTime CreatedAtUtc,
    int Version);
