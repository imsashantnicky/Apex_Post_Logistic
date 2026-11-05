using System.Text.RegularExpressions;

namespace ApexPost.Courier.Domain.Scans;

/// <summary>
/// The formats of tracking numbers and courier ids. The offline store, the API and the app all check
/// against these patterns, so a scan accepted on the device is also accepted by the server.
/// </summary>
public static partial class ScanRules
{
    /// <summary>A tracking number: <c>APX-</c> followed by eight digits, for example <c>APX-12345678</c>.</summary>
    public const string TrackingNumberPattern = @"^APX-\d{8}$";

    /// <summary>A courier id: <c>CR-</c> followed by three digits, for example <c>CR-001</c>.</summary>
    public const string CourierIdPattern = @"^CR-\d{3}$";

    /// <summary>True when <paramref name="trackingNumber"/> matches <see cref="TrackingNumberPattern"/>.</summary>
    public static bool IsValidTrackingNumber(string? trackingNumber) =>
        trackingNumber is not null && TrackingNumberRegex().IsMatch(trackingNumber);

    /// <summary>True when <paramref name="courierId"/> matches <see cref="CourierIdPattern"/>.</summary>
    public static bool IsValidCourierId(string? courierId) =>
        courierId is not null && CourierIdRegex().IsMatch(courierId);

    // ECMAScript keeps \d to the ASCII digits 0-9.
    [GeneratedRegex(TrackingNumberPattern, RegexOptions.ECMAScript)]
    private static partial Regex TrackingNumberRegex();

    [GeneratedRegex(CourierIdPattern, RegexOptions.ECMAScript)]
    private static partial Regex CourierIdRegex();
}
