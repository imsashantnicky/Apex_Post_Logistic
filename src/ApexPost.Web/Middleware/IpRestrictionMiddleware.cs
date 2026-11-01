using System.Net;
using ApexPost.Web.Security;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Middleware;

/// <summary>
/// Answers 403 when an endpoint marked <see cref="IpRestrictedAttribute"/> is called from an address that is not
/// on its allowlist. An unknown address is blocked.
/// </summary>
public sealed partial class IpRestrictionMiddleware(
    RequestDelegate next,
    IOptions<SecuritySettings> settings,
    ILogger<IpRestrictionMiddleware> logger)
{
    private readonly HashSet<IPAddress> _admin = Parse(settings.Value.AdminAllowedIps);
    private readonly HashSet<IPAddress> _integration = Parse(settings.Value.IntegrationAllowedIps);

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var restricted = context.GetEndpoint()?.Metadata.GetMetadata<IpRestrictedAttribute>();
        if (restricted is null || IsAllowed(restricted.Allowlist, context.Connection.RemoteIpAddress))
        {
            await next(context);
            return;
        }

        LogBlocked(context.Request.Path, restricted.Allowlist);
        await ProblemResponses.WriteAsync(context, StatusCodes.Status403Forbidden);
    }

    private static HashSet<IPAddress> Parse(IEnumerable<string> addresses) =>
        addresses.Select(IPAddress.Parse).Select(Normalise).ToHashSet();

    private static IPAddress Normalise(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private bool IsAllowed(IpAllowlist allowlist, IPAddress? address)
    {
        var allowed = allowlist == IpAllowlist.Admin ? _admin : _integration;
        return address is not null && allowed.Contains(Normalise(address));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Blocked a call to {Path}: not on the {Allowlist} allowlist")]
    private partial void LogBlocked(string path, IpAllowlist allowlist);
}
