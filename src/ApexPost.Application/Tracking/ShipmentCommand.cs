namespace ApexPost.Application.Tracking;

/// <summary>The recipient and weight of a shipment, used when it is created or replaced.</summary>
public sealed record ShipmentCommand(
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string RecipientCity,
    decimal WeightKg);
