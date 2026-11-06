using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V2;

/// <summary>The recipient of a shipment in a v2 request, as one nested object.</summary>
public sealed class RecipientRequest
{
    /// <summary>Who receives the shipment.</summary>
    /// <example>Kim Example</example>
    [Required]
    [StringLength(Recipient.NameLength)]
    public string Name { get; init; } = string.Empty;

    /// <summary>The phone number: 7 to 15 digits or spaces, optionally starting with +.</summary>
    /// <example>+44 20 7946 0111</example>
    [Required]
    [RegularExpression(ContractRules.PhonePattern, ErrorMessage = ContractRules.PhoneMessage)]
    public string Phone { get; init; } = string.Empty;

    /// <summary>The street address.</summary>
    /// <example>5 Dock Road</example>
    [Required]
    [StringLength(Recipient.AddressLength)]
    public string Address { get; init; } = string.Empty;

    /// <summary>The city.</summary>
    /// <example>Harbour Point</example>
    [Required]
    [StringLength(Recipient.CityLength)]
    public string City { get; init; } = string.Empty;
}
