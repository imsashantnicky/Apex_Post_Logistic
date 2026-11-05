using System.ComponentModel.DataAnnotations;
using ApexPost.Api.Contracts.Validation;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>
/// One scan in an upload, with the field names of the courier device document. A Delivered scan also needs
/// <see cref="RecipientSignatureName"/>; the name is checked here and not stored.
/// </summary>
public sealed class ScanItem : IValidatableObject
{
    /// <summary>The device document id, scan:: followed by a GUID; it makes a repeated upload a duplicate.</summary>
    [Required]
    [RegularExpression(ContractRules.ScanIdPattern, ErrorMessage = ContractRules.ScanIdMessage)]
    public string ScanId { get; init; } = string.Empty;

    /// <summary>The scanned shipment.</summary>
    [Required]
    [RegularExpression(ContractRules.TrackingNumberPattern, ErrorMessage = ContractRules.TrackingNumberMessage)]
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>What the courier recorded (1 Pickup, 2 InTransit, 3 Delivered, 4 Failed).</summary>
    [Required]
    [EnumDataType(typeof(ScanType))]
    public ScanType? ScanType { get; init; }

    /// <summary>The courier, CR- followed by 3 digits.</summary>
    [Required]
    [RegularExpression(ContractRules.CourierIdPattern, ErrorMessage = ContractRules.CourierIdMessage)]
    public string CourierId { get; init; } = string.Empty;

    /// <summary>Where the scan happened.</summary>
    [Required]
    [StringLength(TrackingScan.LocationLength)]
    public string Location { get; init; } = string.Empty;

    /// <summary>When the scan happened, in UTC; at most 2 minutes ahead of the server clock.</summary>
    [Required]
    [NotInFuture]
    public DateTime? ScannedAtUtc { get; init; }

    /// <summary>Free text from the courier.</summary>
    [StringLength(TrackingScan.NotesLength)]
    public string? Notes { get; init; }

    /// <summary>Who signed for the shipment; required when <see cref="ScanType"/> is Delivered.</summary>
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
