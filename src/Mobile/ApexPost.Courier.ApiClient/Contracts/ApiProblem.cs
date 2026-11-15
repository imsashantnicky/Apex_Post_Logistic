namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>
/// RFC 9457 ProblemDetails body. One type reads both ProblemDetails and ValidationProblemDetails;
/// every member is optional because an error body may be partial.
/// </summary>
public sealed class ApiProblem
{
    /// <summary>Problem type URI.</summary>
    public string? Type { get; init; }

    /// <summary>Short summary, for example "Not Found".</summary>
    public string? Title { get; init; }

    /// <summary>HTTP status code repeated in the body.</summary>
    public int? Status { get; init; }

    /// <summary>Explanation for this occurrence.</summary>
    public string? Detail { get; init; }

    /// <summary>The request path that failed.</summary>
    public string? Instance { get; init; }

    /// <summary>Trace id the API adds to every problem; quote it when reporting an error.</summary>
    public string? TraceId { get; init; }

    /// <summary>Field errors of a ValidationProblemDetails body, or <see langword="null"/>.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
