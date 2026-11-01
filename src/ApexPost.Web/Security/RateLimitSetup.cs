using System.Globalization;
using System.Threading.RateLimiting;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Security;

/// <summary>
/// The login policy counts per client address and the partner policy per partner, both in one-minute windows.
/// A rejected call gets 429 with Retry-After.
/// </summary>
public sealed class RateLimitSetup(IOptions<RateLimitSettings> settings) : IConfigureOptions<RateLimiterOptions>
{
    private const string UnknownCaller = "unknown";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public void Configure(RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = RejectAsync;
        options.AddPolicy(ApexRateLimits.Login, context => FixedWindow(
            context.Connection.RemoteIpAddress?.ToString(),
            settings.Value.LoginPermitsPerMinute));
        options.AddPolicy(ApexRateLimits.Partner, context => FixedWindow(
            PartnerIdentity.Of(context),
            settings.Value.PartnerPermitsPerMinute));
    }

    private static RateLimitPartition<string> FixedWindow(string? caller, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(caller ?? UnknownCaller, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = Window,
            QueueLimit = 0,
        });

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var wait = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : Window;
        var response = context.HttpContext.Response;
        response.Headers.RetryAfter = Math.Ceiling(wait.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        await ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests);
    }
}
