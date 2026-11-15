using ApexPost.Courier.ApiClient.Results;

namespace ApexPost.Courier.Presentation.Errors;

/// <summary>
/// Text for the screen from a failed API call. The client has already turned the ProblemDetails body into a
/// friendly message (server details and token hints never reach it); this adds the trace id as a reference the
/// courier can quote to support.
/// </summary>
public static class FriendlyError
{
    /// <summary>Returns the message, followed by "Reference: …" when the server sent a trace id.</summary>
    /// <param name="failure">The failed call.</param>
    public static string Describe(ApiFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return string.IsNullOrWhiteSpace(failure.TraceId)
            ? failure.Message
            : $"{failure.Message}{Environment.NewLine}Reference: {failure.TraceId}";
    }
}
