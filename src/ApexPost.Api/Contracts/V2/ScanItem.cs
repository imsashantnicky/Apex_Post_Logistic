using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApexPost.Api.Contracts.Validation;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V2;

/// <summary>
/// One scan in a v2 upload: the courier device document, with <see cref="ScanType"/> by name as the device stores it.
/// A Delivered scan also needs <see cref="RecipientSignatureName"/>; the name is checked here and not stored.
/// </summary>
public sealed class ScanItem : IValidatableObject
{
    /// <summary>The device document id, scan:: followed by a GUID; it makes a repeated upload a duplicate.</summary>
    /// <example>scan::0f8fad5b-d9cb-469f-a165-70867728950e</example>
    [Required]
    [RegularExpression(ContractRules.ScanIdPattern, ErrorMessage = ContractRules.ScanIdMessage)]
    public string ScanId { get; init; } = string.Empty;

    /// <summary>The scanned shipment.</summary>
    /// <example>APX-20000001</example>
    [Required]
    [RegularExpression(ContractRules.TrackingNumberPattern, ErrorMessage = ContractRules.TrackingNumberMessage)]
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>What the courier recorded: Pickup, InTransit, Delivered or Failed.</summary>
    /// <example>Pickup</example>
    [Required]
    [EnumDataType(typeof(ScanType))]
    [JsonConverter(typeof(JsonStringEnumConverter<ScanType>))]
    public ScanType? ScanType { get; init; }

    /// <summary>The courier, CR- followed by 3 digits.</summary>
    /// <example>CR-101</example>
    [Required]
    [RegularExpression(ContractRules.CourierIdPattern, ErrorMessage = ContractRules.CourierIdMessage)]
    public string CourierId { get; init; } = string.Empty;

    /// <summary>Where the scan happened.</summary>
    /// <example>Harbour Point depot</example>
    [Required]
    [StringLength(TrackingScan.LocationLength)]
    public string Location { get; init; } = string.Empty;

    /// <summary>When the scan happened, in UTC; at most 2 minutes ahead of the server clock.</summary>
    /// <example>2026-11-05T09:30:00.0000000Z</example>
    [Required]
    [NotInFuture]
    public DateTime? ScannedAtUtc { get; init; }

    /// <summary>Free text from the courier.</summary>
    /// <example>Collected from reception.</example>
    [StringLength(TrackingScan.NotesLength)]
    public string? Notes { get; init; }

    /// <summary>Who signed for the shipment; required when <see cref="ScanType"/> is Delivered.</summary>
    /// <example>Ava Example</example>
    [StringLength(ContractRules.SignatureNameLength)]
    public string? RecipientSignatureName { get; init; }

    /// <summary>The rule across fields: a Delivered scan names who signed for it.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScanType == Domain.Tracking.ScanType.Delivered && string.IsNullOrWhiteSpace(RecipientSignatureName))
        {
            yield return new ValidationResult(ContractRules.SignatureMessage, [nameof(RecipientSignatureName)]);
        }
    }
}
