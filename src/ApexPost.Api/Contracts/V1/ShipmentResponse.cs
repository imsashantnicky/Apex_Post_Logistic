using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>A shipment in v1: flat recipient fields and the status as a number.</summary>
/// <param name="TrackingNumber">The tracking number.</param>
/// <param name="RecipientName">Who receives the shipment.</param>
/// <param name="RecipientPhone">The recipient's phone number.</param>
/// <param name="RecipientAddress">The street address.</param>
/// <param name="RecipientCity">The city.</param>
/// <param name="WeightKg">The weight in kilograms.</param>
/// <param name="Status">The current status.</param>
/// <param name="CreatedAtUtc">When the shipment was registered.</param>
/// <param name="Version">The change counter.</param>
public sealed record ShipmentResponse(
    string TrackingNumber,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string RecipientCity,
    decimal WeightKg,
    ShipmentStatus Status,
    DateTime CreatedAtUtc,
    int Version)
{
    /// <summary>The tracking number, APX- followed by 8 digits.</summary>
    /// <example>APX-20000001</example>
    public string TrackingNumber { get; init; } = TrackingNumber;

    /// <summary>Who receives the shipment.</summary>
    /// <example>Ava Example</example>
    public string RecipientName { get; init; } = RecipientName;

    /// <summary>The recipient's phone number.</summary>
    /// <example>+44 20 7946 0101</example>
    public string RecipientPhone { get; init; } = RecipientPhone;

    /// <summary>The street address.</summary>
    /// <example>12 Quay Street</example>
    public string RecipientAddress { get; init; } = RecipientAddress;

    /// <summary>The city.</summary>
    /// <example>Harbour Point</example>
    public string RecipientCity { get; init; } = RecipientCity;

    /// <summary>The weight in kilograms.</summary>
    /// <example>1.2</example>
    public decimal WeightKg { get; init; } = WeightKg;

    /// <summary>
    /// The current status as a number: 1 Created, 2 PickedUp, 3 InTransit, 4 OutForDelivery, 5 Delivered, 6 Failed.
    /// </summary>
    /// <example>1</example>
    public ShipmentStatus Status { get; init; } = Status;

    /// <summary>When the shipment was registered, in UTC.</summary>
    /// <example>2026-11-05T08:00:00.0000000Z</example>
    public DateTime CreatedAtUtc { get; init; } = CreatedAtUtc;

    /// <summary>A counter that grows with every change of the shipment.</summary>
    /// <example>1</example>
    public int Version { get; init; } = Version;
}
