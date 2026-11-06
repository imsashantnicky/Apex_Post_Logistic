using System.Net;
using ApexPost.Courier.ApiClient.Contracts;

namespace ApexPost.Courier.ApiClient.Results;

/// <summary>Turns an HTTP error and its ProblemDetails body into a friendly <see cref="ApiFailure"/>.</summary>
public static class FailureMessages
{
    /// <summary>Text for a request that could not reach the server.</summary>
    public const string Network = "Cannot reach the ApexPost server. Your scans stay on this device.";

    /// <summary>Text for a request that took too long.</summary>
    public const string Timeout = "The ApexPost server took too long to answer. Try again.";

    /// <summary>Text for a body the app could not read.</summary>
    public const string InvalidResponse = "The server sent an answer the app could not read.";

    /// <summary>Text for a tracking number that is not <c>APX-</c> plus eight digits.</summary>
    public const string InvalidTrackingNumber = "Enter a tracking number like APX-10000001.";

    private const int MaximumFieldErrorsShown = 3;

    /// <summary>Builds the failure for an error status, using the problem body when there is one.</summary>
    /// <param name="status">HTTP status of the answer.</param>
    /// <param name="problem">The parsed ProblemDetails, or <see langword="null"/> when the body was unusable.</param>
    public static ApiFailure FromProblem(HttpStatusCode status, ApiProblem? problem)
    {
        var fieldErrors = Clean(problem?.Errors);
        var kind = KindOf(status);
        var message = fieldErrors.Count > 0
            ? DescribeFieldErrors(fieldErrors)
            : Describe(kind, problem?.Detail);

        return new ApiFailure(kind, message, (int)status, problem?.TraceId) { FieldErrors = fieldErrors };
    }

    /// <summary>Maps an HTTP error status to a failure kind.</summary>
    /// <param name="status">HTTP status of the answer.</param>
    public static ApiFailureKind KindOf(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => ApiFailureKind.Validation,
        HttpStatusCode.Unauthorized => ApiFailureKind.Unauthorized,
        HttpStatusCode.Forbidden => ApiFailureKind.Forbidden,
        HttpStatusCode.NotFound => ApiFailureKind.NotFound,
        HttpStatusCode.Conflict => ApiFailureKind.Conflict,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ApiFailureKind.Timeout,
        >= HttpStatusCode.InternalServerError => ApiFailureKind.Server,
        _ => ApiFailureKind.Rejected,
    };

    // The server's detail is shown for client errors (it explains what to fix); server errors and auth
    // failures get fixed texts so internal details or token hints never reach the screen.
    private static string Describe(ApiFailureKind kind, string? detail) => kind switch
    {
        ApiFailureKind.Unauthorized => "Your sign-in has expired or is missing. Sign in again.",
        ApiFailureKind.Forbidden => "Your account is not allowed to do this.",
        ApiFailureKind.Server => "The ApexPost server had a problem. Try again later.",
        ApiFailureKind.Timeout => Timeout,
        _ when !string.IsNullOrWhiteSpace(detail) => detail.Trim(),
        ApiFailureKind.NotFound => "No shipment was found for that tracking number.",
        ApiFailureKind.Conflict => "The shipment changed or the change is not allowed. Refresh and try again.",
        ApiFailureKind.Validation => "The server did not accept the data. Check the fields and try again.",
        _ => "The server refused the request.",
    };

    private static string DescribeFieldErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        var lines = errors
            .Take(MaximumFieldErrorsShown)
            .Select(pair => pair.Value.Length > 0 ? pair.Value[0] : pair.Key);
        return "Please check: " + string.Join(" ", lines);
    }

    // Drops null keys, values and messages that a careless server might send.
    private static Dictionary<string, string[]> Clean(IReadOnlyDictionary<string, string[]>? errors)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (errors is null)
        {
            return result;
        }

        foreach (var (field, messages) in errors)
        {
            var kept = (messages ?? []).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
            result[field] = kept;
        }

        return result;
    }
}
