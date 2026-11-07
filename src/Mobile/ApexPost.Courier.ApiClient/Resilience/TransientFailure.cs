using System.Globalization;
using System.Net;
using Polly;
using Polly.Timeout;

namespace ApexPost.Courier.ApiClient.Resilience;

/// <summary>
/// The outcomes that say "the server or the network is in trouble": a network exception, an attempt timeout,
/// 408 Request Timeout or any 5xx. Any other 4xx answer is the server's final word and never counts.
/// </summary>
public static class TransientFailure
{
    /// <summary>True when <paramref name="outcome"/> is a transient failure.</summary>
    /// <param name="outcome">Result or exception of one attempt.</param>
    public static bool Is(Outcome<HttpResponseMessage> outcome) => outcome switch
    {
        { Exception: HttpRequestException or TimeoutRejectedException } => true,
        { Result: { } response } => IsTransientStatus(response.StatusCode),
        _ => false,
    };

    /// <summary>True for 408 and every 5xx status.</summary>
    /// <param name="status">HTTP status of an answer.</param>
    public static bool IsTransientStatus(HttpStatusCode status) =>
        status == HttpStatusCode.RequestTimeout || (int)status >= 500;

    /// <summary>Short reason for a log line: the status code or the exception type, never a body or a header.</summary>
    /// <param name="outcome">Result or exception of one attempt.</param>
    public static string Describe(Outcome<HttpResponseMessage> outcome) => outcome switch
    {
        { Exception: { } exception } => exception.GetType().Name,
        { Result: { } response } => string.Create(CultureInfo.InvariantCulture, $"HTTP {(int)response.StatusCode}"),
        _ => "unknown",
    };
}
