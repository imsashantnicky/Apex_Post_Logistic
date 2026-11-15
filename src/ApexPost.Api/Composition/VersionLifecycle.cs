namespace ApexPost.Api.Composition;

/// <summary>
/// The fictional lifecycle of v1, in one place for the response headers, the OpenAPI description and VERSIONING.md:
/// deprecated on <see cref="V1DeprecatedOn"/>, removed (sunset) on <see cref="V1SunsetOn"/>.
/// </summary>
public static class VersionLifecycle
{
    /// <summary>The day v1 was deprecated (the Deprecation header).</summary>
    public static readonly DateOnly V1DeprecatedOn = new(2026, 11, 5);

    /// <summary>The day v1 stops answering (the Sunset header).</summary>
    public static readonly DateOnly V1SunsetOn = new(2027, 5, 31);

    /// <summary>The fictional page with the v1 → v2 migration guide, sent in the Link header.</summary>
    public const string MigrationGuideUrl = "https://developer.apexpost.example/api/versioning";

    /// <summary>A date as midnight UTC, the form the versioning policies take.</summary>
    public static DateTimeOffset AtMidnightUtc(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
