using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts;

/// <summary>
/// The patterns, limits and messages of the request contracts. The tracking number and courier id patterns are the
/// domain ones, so the API and the courier device check the same format.
/// </summary>
public static class ContractRules
{
    public const string TrackingNumberPattern = TrackingFormats.TrackingNumberPattern;
    public const string TrackingNumberMessage = "Tracking number must be APX- followed by 8 digits, e.g. APX-20000001.";

    public const string CourierIdPattern = TrackingFormats.CourierIdPattern;
    public const string CourierIdMessage = "Courier id must be CR- followed by 3 digits, e.g. CR-101.";

    public const string ScanIdPattern = "^scan::[0-9a-fA-F-]{36}$";
    public const string ScanIdMessage =
        "Scan id must be scan:: followed by a GUID, e.g. scan::0f8fad5b-d9cb-469f-a165-70867728950e.";

    public const string PhonePattern = @"^\+?[0-9 ]{7,15}$";
    public const string PhoneMessage =
        "Phone must be 7 to 15 digits or spaces, optionally starting with +, e.g. +44 20 7946 0101.";

    // Kept as text so RangeAttribute compares decimals exactly; the maximum is the domain's 30 kg.
    public const string MinimumWeightKg = "0.1";
    public const string MaximumWeightKg = "30";
    public const string WeightMessage = "Weight must be between 0.1 and 30 kg.";

    public const int SignatureNameLength = Recipient.NameLength;
    public const string SignatureMessage = "A Delivered scan needs the name of the person who signed for it.";
}
