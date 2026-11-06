namespace ApexPost.Courier.ApiClient.Results;

/// <summary>A failed API call, with a message that can be shown to the courier as it is.</summary>
/// <param name="Kind">What went wrong.</param>
/// <param name="Message">Friendly, user-facing text.</param>
/// <param name="StatusCode">HTTP status, or <see langword="null"/> when no answer arrived.</param>
/// <param name="TraceId">Trace id from the ProblemDetails body, for support.</param>
public sealed record ApiFailure(ApiFailureKind Kind, string Message, int? StatusCode = null, string? TraceId = null)
{
    /// <summary>Field errors from a ValidationProblemDetails body (empty when there were none).</summary>
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; init; } = new Dictionary<string, string[]>();
}
