using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>The body of POST /api/v1/shipments: a new shipment, which starts as Created.</summary>
public sealed class CreateShipmentRequest
{
    /// <summary>The tracking number, APX- followed by 8 digits.</summary>
    [Required]
    [RegularExpression(ContractRules.TrackingNumberPattern, ErrorMessage = ContractRules.TrackingNumberMessage)]
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>Who receives the shipment.</summary>
    [Required]
    [StringLength(Recipient.NameLength)]
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>The recipient's phone number.</summary>
    [Required]
    [RegularExpression(ContractRules.PhonePattern, ErrorMessage = ContractRules.PhoneMessage)]
    public string RecipientPhone { get; init; } = string.Empty;

    /// <summary>The street address.</summary>
    [Required]
    [StringLength(Recipient.AddressLength)]
    public string RecipientAddress { get; init; } = string.Empty;

    /// <summary>The city.</summary>
    [Required]
    [StringLength(Recipient.CityLength)]
    public string RecipientCity { get; init; } = string.Empty;

    /// <summary>The weight in kilograms, 0.1 to 30.</summary>
    [Range(
        typeof(decimal),
        ContractRules.MinimumWeightKg,
        ContractRules.MaximumWeightKg,
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = ContractRules.WeightMessage)]
    public decimal WeightKg { get; init; }
}
