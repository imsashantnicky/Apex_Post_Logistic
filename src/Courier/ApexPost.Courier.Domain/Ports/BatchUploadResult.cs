namespace ApexPost.Courier.Domain.Ports;

/// <summary>The outcome of one batch upload.</summary>
/// <param name="Outcome">How the batch ended.</param>
/// <param name="Rejected">For a completed batch: the refused scan ids and their reasons.</param>
/// <param name="Error">Why the batch did not complete; <see langword="null"/> when it did.</param>
public sealed record BatchUploadResult(
    BatchUploadOutcome Outcome,
    IReadOnlyDictionary<string, string> Rejected,
    string? Error)
{
    private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>();

    /// <summary>The server answered; <paramref name="rejected"/> maps refused scan ids to reasons.</summary>
    public static BatchUploadResult Completed(IReadOnlyDictionary<string, string> rejected) =>
        new(BatchUploadOutcome.Completed, rejected, null);

    /// <summary>The server refused the whole batch for <paramref name="reason"/>.</summary>
    public static BatchUploadResult Refused(string reason) => new(BatchUploadOutcome.Refused, None, reason);

    /// <summary>The batch could not be delivered for <paramref name="reason"/>; try again later.</summary>
    public static BatchUploadResult Retryable(string reason) => new(BatchUploadOutcome.Retryable, None, reason);

    /// <summary>The server needs a new sign-in.</summary>
    public static BatchUploadResult Unauthorized(string reason) =>
        new(BatchUploadOutcome.Unauthorized, None, reason);
}
