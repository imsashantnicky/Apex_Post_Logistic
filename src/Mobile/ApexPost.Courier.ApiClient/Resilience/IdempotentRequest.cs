namespace ApexPost.Courier.ApiClient.Resilience;

/// <summary>
/// Decides whether a request may be sent again. GET, HEAD, OPTIONS, PUT and DELETE are idempotent by HTTP
/// semantics. Any other request is retried only when the caller marked it, as the scan upload does: every scan
/// carries its <c>scan::{Guid}</c> id and the server counts a repeated id as a duplicate.
/// </summary>
public static class IdempotentRequest
{
    /// <summary>Request option that marks a non-idempotent method as safe to repeat.</summary>
    public static readonly HttpRequestOptionsKey<bool> Key = new("ApexPost.RetrySafe");

    /// <summary>Marks <paramref name="request"/> as safe to send more than once.</summary>
    /// <param name="request">The request to mark.</param>
    public static void Mark(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Options.Set(Key, true);
    }

    /// <summary>True when the retry strategy may send <paramref name="request"/> again.</summary>
    /// <param name="request">The request, or <see langword="null"/> when the context has none.</param>
    public static bool IsRetrySafe(HttpRequestMessage? request) =>
        request is not null
        && (IsIdempotentMethod(request.Method) || (request.Options.TryGetValue(Key, out var marked) && marked));

    /// <summary>True for the methods HTTP defines as idempotent.</summary>
    /// <param name="method">The HTTP method.</param>
    public static bool IsIdempotentMethod(HttpMethod method) =>
        method == HttpMethod.Get
        || method == HttpMethod.Head
        || method == HttpMethod.Options
        || method == HttpMethod.Put
        || method == HttpMethod.Delete;
}
