using System.Security.Cryptography;
using System.Text;

namespace ApexPost.Api.Security;

/// <summary>
/// The partner API keys from the <c>PartnerKeys</c> configuration section (user-secrets or environment variables,
/// never a committed file): one entry per partner, <c>PartnerKeys:&lt;PartnerName&gt; = &lt;key&gt;</c>. The section
/// is read on every check, so a rotated key takes effect as soon as the configuration reloads.
/// </summary>
public sealed class PartnerKeyStore(IConfiguration configuration)
{
    /// <summary>The configuration section that holds the keys.</summary>
    public const string SectionName = "PartnerKeys";

    /// <summary>
    /// The partner whose key matches, or null. Both sides are hashed with SHA-256 and compared with
    /// <see cref="CryptographicOperations.FixedTimeEquals"/>, and every configured key is compared, so the time taken
    /// reveals neither the content nor the length of a key, nor which partner almost matched.
    /// </summary>
    public string? FindPartner(string? presentedKey)
    {
        if (string.IsNullOrEmpty(presentedKey))
        {
            return null;
        }

        var presented = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));
        string? partner = null;
        foreach (var entry in configuration.GetSection(SectionName).GetChildren())
        {
            if (string.IsNullOrEmpty(entry.Value))
            {
                continue;
            }

            var expected = SHA256.HashData(Encoding.UTF8.GetBytes(entry.Value));
            if (CryptographicOperations.FixedTimeEquals(presented, expected))
            {
                partner ??= entry.Key;
            }
        }

        return partner;
    }
}
