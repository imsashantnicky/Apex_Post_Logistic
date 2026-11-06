using System.ComponentModel.DataAnnotations;
using ApexPost.Api.Contracts;

namespace ApexPost.Api.Security;

/// <summary>The body of POST /dev/token.</summary>
public sealed class DevTokenRequest
{
    /// <summary>A fictional courier id: CR-101 or CR-102.</summary>
    [Required]
    [RegularExpression(ContractRules.CourierIdPattern, ErrorMessage = ContractRules.CourierIdMessage)]
    public string? CourierId { get; init; }
}
