namespace ApexPost.Application.Settings;

/// <summary>The "Courier" section: timeout and concurrency limit for the courier tracking service.</summary>
public sealed class CourierSettings
{
    public const string SectionName = "Courier";

    public int TimeoutMilliseconds { get; set; } = 2000;

    public int MaxConcurrentRequests { get; set; } = 3;
}
