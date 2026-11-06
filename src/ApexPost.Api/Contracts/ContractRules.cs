using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts;

/// <summary>
/// The patterns, limits and messages of the request contracts. The tracking number and courier id patterns are the
/// domain ones, so the API and the courier device check the same format.
/// </summary>
public static class ContractRules
{
    /// <summary>A tracking number: APX- followed by 8 digits (the domain pattern).</summary>
    public const string TrackingNumberPattern = TrackingFormats.TrackingNumberPattern;

    /// <summary>The validation message of a wrong tracking number.</summary>
    public const string TrackingNumberMessage = "Tracking number must be APX- followed by 8 digits, e.g. APX-20000001.";

    /// <summary>A courier id: CR- followed by 3 digits (the domain pattern).</summary>
    public const string CourierIdPattern = TrackingFormats.CourierIdPattern;

    /// <summary>The validation message of a wrong courier id.</summary>
    public const string CourierIdMessage = "Courier id must be CR- followed by 3 digits, e.g. CR-101.";

    /// <summary>A scan document id: scan:: followed by a GUID.</summary>
    public const string ScanIdPattern = "^scan::[0-9a-fA-F-]{36}$";

    /// <summary>The validation message of a wrong scan id.</summary>
    public const string ScanIdMessage =
        "Scan id must be scan:: followed by a GUID, e.g. scan::0f8fad5b-d9cb-469f-a165-70867728950e.";

    /// <summary>A phone number: 7 to 15 digits or spaces, optionally starting with +.</summary>
    public const string PhonePattern = @"^\+?[0-9 ]{7,15}$";

    /// <summary>The validation message of a wrong phone number.</summary>
    public const string PhoneMessage =
        "Phone must be 7 to 15 digits or spaces, optionally starting with +, e.g. +44 20 7946 0101.";

    /// <summary>
    /// The lightest weight in kg. Kept as text so RangeAttribute compares decimals exactly.
    /// </summary>
    public const string MinimumWeightKg = "0.1";

    /// <summary>The heaviest weight in kg, the domain's 30 kg; text for the same reason.</summary>
    public const string MaximumWeightKg = "30";

    /// <summary>The validation message of a weight out of range.</summary>
    public const string WeightMessage = "Weight must be between 0.1 and 30 kg.";

    /// <summary>The longest signature name, the same as a recipient name.</summary>
    public const int SignatureNameLength = Recipient.NameLength;

    /// <summary>The validation message of a Delivered scan without a signature name.</summary>
    public const string SignatureMessage = "A Delivered scan needs the name of the person who signed for it.";
}
