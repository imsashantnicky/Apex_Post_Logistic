namespace ApexPost.Application.Settings;

/// <summary>
/// The "Seed" section. Demo data is created only in Development when Enabled is true;
/// the demo password comes from user-secrets, never from source control.
/// </summary>
public sealed class SeedSettings
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }

    public string DemoPassword { get; set; } = string.Empty;
}
