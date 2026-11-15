using ApexPost.Api.Security;

namespace ApexPost.Api.Tests.Support;

/// <summary>Fictional partner keys that <see cref="ApiFactory"/> puts into the PartnerKeys section.</summary>
public static class TestPartnerKeys
{
    public const string Partner = "ParcelTrackPartner";
    public const string Key = "test-partner-key-1c6f0b6e";
    public const string OtherPartner = "OtherPartner";
    public const string OtherKey = "test-partner-key-9a2d47c3";

    public static IReadOnlyDictionary<string, string?> HostConfiguration { get; } = new Dictionary<string, string?>
    {
        [$"{PartnerKeyStore.SectionName}:{Partner}"] = Key,
        [$"{PartnerKeyStore.SectionName}:{OtherPartner}"] = OtherKey,
        [$"{PartnerKeyStore.SectionName}:DisabledPartner"] = string.Empty,
    };
}
