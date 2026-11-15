using System.ComponentModel.DataAnnotations;

namespace ApexPost.Api.Contracts.V2;

/// <summary>The body of POST /api/v2/shipments: a new shipment, which starts as Created.</summary>
public sealed class CreateShipmentRequest
{
    /// <summary>The tracking number, APX- followed by 8 digits. It must not exist yet.</summary>
    /// <example>APX-20000011</example>
    [Required]
    [RegularExpression(ContractRules.TrackingNumberPattern, ErrorMessage = ContractRules.TrackingNumberMessage)]
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>Who receives the shipment and where.</summary>
    [Required]
    public RecipientRequest? Recipient { get; init; }

    /// <summary>The weight in kilograms, 0.1 to 30.</summary>
    /// <example>2.5</example>
    [Range(
        typeof(decimal),
        ContractRules.MinimumWeightKg,
        ContractRules.MaximumWeightKg,
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = ContractRules.WeightMessage)]
    public decimal WeightKg { get; init; }
}
