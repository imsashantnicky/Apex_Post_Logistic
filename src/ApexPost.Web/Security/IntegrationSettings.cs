namespace ApexPost.Web.Security;

/// <summary>
/// The "Integration" section: the browser origins of partner widgets, and one API key per partner. The keys come
/// from user-secrets or the environment, never from a file in source control.
/// </summary>
public sealed class IntegrationSettings
{
    public const string SectionName = "Integration";

    public string[] AllowedOrigins { get; set; } = [];

    public Dictionary<string, string> ApiKeys { get; set; } = [];
}
