namespace ApexPost.Courier.Domain.Ports;

/// <summary>How a batch upload ended, which decides the new sync status of its scans.</summary>
public enum BatchUploadOutcome
{
    /// <summary>The server answered the batch: rejected scans become Failed, all others Synced.</summary>
    Completed = 1,

    /// <summary>The server refused the whole batch (for example 400, 403 or 404): every scan becomes Failed.</summary>
    Refused = 2,

    /// <summary>
    /// The server could not be reached or failed (network, timeout, 5xx, open circuit): the scans stay as they are
    /// and are sent again on the next run.
    /// </summary>
    Retryable = 3,

    /// <summary>401: the token is missing or expired. The scans stay as they are; the courier signs in again.</summary>
    Unauthorized = 4,
}
