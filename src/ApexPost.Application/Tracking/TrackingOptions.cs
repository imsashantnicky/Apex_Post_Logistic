namespace ApexPost.Application.Tracking;

/// <summary>The "Tracking" configuration section: tracking limits that a deployment may change.</summary>
public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";

    public const int DefaultMaxScanAgeHours = 72;

    /// <summary>
    /// How many hours before the upload time a scan may have been made. A scan exactly this old is still accepted;
    /// an older one is rejected. Must be 1 or more.
    /// </summary>
    public int MaxScanAgeHours { get; set; } = DefaultMaxScanAgeHours;
}
