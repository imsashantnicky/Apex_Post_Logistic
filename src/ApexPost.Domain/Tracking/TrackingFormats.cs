using System.Text.RegularExpressions;

namespace ApexPost.Domain.Tracking;

/// <summary>
/// The formats of tracking numbers, courier ids and scan ids. They equal <c>ScanRules</c> and <c>ScanIds</c> of the
/// courier device; the Domain references no other assembly, so a unit test keeps the two copies identical.
/// </summary>
public static partial class TrackingFormats
{
    /// <summary><c>APX-</c> followed by eight digits, for example <c>APX-10000001</c>.</summary>
    public const string TrackingNumberPattern = @"^APX-\d{8}$";

    /// <summary><c>CR-</c> followed by three digits, for example <c>CR-101</c>.</summary>
    public const string CourierIdPattern = @"^CR-\d{3}$";

    /// <summary>The prefix of a scan id <c>scan::{Guid}</c>.</summary>
    public const string ScanIdPrefix = "scan::";

    /// <summary>Length of a tracking number.</summary>
    public const int TrackingNumberLength = 12;

    /// <summary>Length of a courier id.</summary>
    public const int CourierIdLength = 6;

    /// <summary>Length of a scan id: the prefix and a GUID in "D" format.</summary>
    public const int ScanIdLength = 42;

    private const string GuidFormat = "D";

    /// <summary>True when <paramref name="value"/> matches <see cref="TrackingNumberPattern"/>.</summary>
    public static bool IsTrackingNumber(string? value) => value is not null && TrackingNumberRegex().IsMatch(value);

    /// <summary>True when <paramref name="value"/> matches <see cref="CourierIdPattern"/>.</summary>
    public static bool IsCourierId(string? value) => value is not null && CourierIdRegex().IsMatch(value);

    /// <summary>True when <paramref name="value"/> is <see cref="ScanIdPrefix"/> followed by a GUID.</summary>
    public static bool IsScanId(string? value) =>
        value is not null
        && value.StartsWith(ScanIdPrefix, StringComparison.Ordinal)
        && Guid.TryParseExact(value[ScanIdPrefix.Length..], GuidFormat, out _);

    // ECMAScript keeps \d to the ASCII digits 0-9.
    [GeneratedRegex(TrackingNumberPattern, RegexOptions.ECMAScript)]
    private static partial Regex TrackingNumberRegex();

    [GeneratedRegex(CourierIdPattern, RegexOptions.ECMAScript)]
    private static partial Regex CourierIdRegex();
}
