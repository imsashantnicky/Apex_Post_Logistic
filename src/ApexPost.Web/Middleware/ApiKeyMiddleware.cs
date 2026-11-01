using System.Security.Cryptography;
using System.Text;
using ApexPost.Web.Security;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Middleware;

/// <summary>
/// Guards endpoints marked <see cref="RequireApiKeyAttribute"/>: 500 when no key is configured, 401 for a missing
/// or wrong key. Both sides are hashed to the same length and every key is compared in constant time, so the
/// answer reveals neither the length of a key nor which partner came closest. The matched partner is kept for the
/// rate limiter.
/// </summary>
public sealed partial class ApiKeyMiddleware(
    RequestDelegate next,
    IOptions<IntegrationSettings> settings,
    ILogger<ApiKeyMiddleware> logger)
{
    private readonly Dictionary<string, byte[]> _keys = settings.Value.ApiKeys
        .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
        .ToDictionary(pair => pair.Key, pair => Hash(pair.Value));

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequireApiKeyAttribute>() is null)
        {
            await next(context);
            return;
        }

        if (_keys.Count == 0)
        {
            LogNotConfigured();
            await ProblemResponses.WriteAsync(context, StatusCodes.Status500InternalServerError);
            return;
        }

        if (FindPartner(context.Request.Headers[ApexHeaders.ApiKey].ToString()) is not { } partner)
        {
            await ProblemResponses.WriteAsync(context, StatusCodes.Status401Unauthorized);
            return;
        }

        PartnerIdentity.Set(context, partner);
        await next(context);
    }

    private static byte[] Hash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    /// <summary>Always looks at every key: leaving the loop early would tell an attacker how far it got.</summary>
    private string? FindPartner(string presentedKey)
    {
        var presented = Hash(presentedKey);
        string? partner = null;
        foreach (var (name, expected) in _keys)
        {
            if (CryptographicOperations.FixedTimeEquals(presented, expected))
            {
                partner = name;
            }
        }

        return partner;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "An API key endpoint was called but no API key is configured")]
    private partial void LogNotConfigured();
}
