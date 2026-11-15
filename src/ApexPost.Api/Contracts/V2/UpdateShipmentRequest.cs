using System.ComponentModel.DataAnnotations;

namespace ApexPost.Api.Contracts.V2;

/// <summary>
/// The body of PUT /api/v2/shipments/{trackingNumber}: the new recipient and weight. The tracking number comes from
/// the route and cannot change.
/// </summary>
public sealed class UpdateShipmentRequest
{
    /// <summary>Who receives the shipment and where.</summary>
    [Required]
    public RecipientRequest? Recipient { get; init; }

    /// <summary>The weight in kilograms, 0.1 to 30.</summary>
    /// <example>1.4</example>
    [Range(
        typeof(decimal),
        ContractRules.MinimumWeightKg,
        ContractRules.MaximumWeightKg,
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = ContractRules.WeightMessage)]
    public decimal WeightKg { get; init; }
}
