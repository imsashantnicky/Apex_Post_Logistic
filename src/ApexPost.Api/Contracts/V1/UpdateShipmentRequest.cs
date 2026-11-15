using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>
/// The body of PUT /api/shipments/{trackingNumber}: the new recipient and weight. The tracking number comes from
/// the route and cannot change.
/// </summary>
public sealed class UpdateShipmentRequest
{
    /// <summary>Who receives the shipment.</summary>
    /// <example>Ava Example</example>
    [Required]
    [StringLength(Recipient.NameLength)]
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>The recipient's phone number: 7 to 15 digits or spaces, optionally starting with +.</summary>
    /// <example>+44 20 7946 0101</example>
    [Required]
    [RegularExpression(ContractRules.PhonePattern, ErrorMessage = ContractRules.PhoneMessage)]
    public string RecipientPhone { get; init; } = string.Empty;

    /// <summary>The street address.</summary>
    /// <example>14 Quay Street</example>
    [Required]
    [StringLength(Recipient.AddressLength)]
    public string RecipientAddress { get; init; } = string.Empty;

    /// <summary>The city.</summary>
    /// <example>Harbour Point</example>
    [Required]
    [StringLength(Recipient.CityLength)]
    public string RecipientCity { get; init; } = string.Empty;

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
