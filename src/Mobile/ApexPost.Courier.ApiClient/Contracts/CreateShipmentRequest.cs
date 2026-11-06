namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>Body of <c>POST /api/v2/shipments</c> (Admin role).</summary>
/// <param name="TrackingNumber">Tracking number, <c>APX-</c> plus eight digits.</param>
/// <param name="Recipient">Who receives the shipment.</param>
/// <param name="WeightKg">Weight in kilograms.</param>
public sealed record CreateShipmentRequest(string TrackingNumber, RecipientRequest Recipient, decimal WeightKg);
