namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>Body of <c>PUT /api/v2/shipments/{trackingNumber}</c> (Admin role).</summary>
/// <param name="Recipient">Who receives the shipment.</param>
/// <param name="WeightKg">Weight in kilograms.</param>
public sealed record UpdateShipmentRequest(RecipientRequest Recipient, decimal WeightKg);
