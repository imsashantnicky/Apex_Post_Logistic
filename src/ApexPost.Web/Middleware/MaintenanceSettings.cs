namespace ApexPost.Web.Middleware;

/// <summary>The "Maintenance" section; read on every request, so a config change applies without a restart.</summary>
public sealed class MaintenanceSettings
{
    public const string SectionName = "Maintenance";

    public bool Enabled { get; set; }

    public int RetryAfterSeconds { get; set; } = 120;
}
