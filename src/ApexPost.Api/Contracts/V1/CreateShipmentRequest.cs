using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>The body of POST /api/shipments: a new shipment, which starts as Created.</summary>
public sealed class CreateShipmentRequest
{
    /// <summary>The tracking number, APX- followed by 8 digits. It must not exist yet.</summary>
    /// <example>APX-20000011</example>
    [Required]
    [RegularExpression(ContractRules.TrackingNumberPattern, ErrorMessage = ContractRules.TrackingNumberMessage)]
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>Who receives the shipment.</summary>
    /// <example>Kim Example</example>
    [Required]
    [StringLength(Recipient.NameLength)]
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>The recipient's phone number: 7 to 15 digits or spaces, optionally starting with +.</summary>
    /// <example>+44 20 7946 0111</example>
    [Required]
    [RegularExpression(ContractRules.PhonePattern, ErrorMessage = ContractRules.PhoneMessage)]
    public string RecipientPhone { get; init; } = string.Empty;

    /// <summary>The street address.</summary>
    /// <example>5 Dock Road</example>
    [Required]
    [StringLength(Recipient.AddressLength)]
    public string RecipientAddress { get; init; } = string.Empty;

    /// <summary>The city.</summary>
    /// <example>Harbour Point</example>
    [Required]
    [StringLength(Recipient.CityLength)]
    public string RecipientCity { get; init; } = string.Empty;

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
