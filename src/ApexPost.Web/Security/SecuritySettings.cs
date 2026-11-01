namespace ApexPost.Web.Security;

/// <summary>The "Security" section: the addresses that may reach restricted endpoints.</summary>
public sealed class SecuritySettings
{
    public const string SectionName = "Security";

    public string[] AdminAllowedIps { get; set; } = [];

    public string[] IntegrationAllowedIps { get; set; } = [];
}
