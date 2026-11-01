namespace ApexPost.Web.Security;

/// <summary>The "RateLimiting" section: requests allowed per minute.</summary>
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public int LoginPermitsPerMinute { get; set; } = 5;

    public int PartnerPermitsPerMinute { get; set; } = 30;
}
