namespace ApexPost.Courier.Offline.Outbox;

/// <summary>The outcome of one outbox run.</summary>
/// <param name="Attempted">How many scans the run loaded to upload.</param>
/// <param name="Synced">How many of them the server accepted or already had; they are now Synced.</param>
/// <param name="Failed">How many of them the server refused; they are now Failed and are retried later.</param>
public sealed record OutboxResult(int Attempted, int Synced, int Failed)
{
    /// <summary>The result of a run that uploaded nothing, for example because another run was busy.</summary>
    public static OutboxResult Empty { get; } = new(0, 0, 0);

    /// <summary>
    /// Scans the run did not send because the server was unreachable or needs a new sign-in. They keep their
    /// status (Pending stays Pending) and go with the next run.
    /// </summary>
    public int Deferred { get; init; }

    /// <summary>True when the server answered 401: the courier has to sign in again before the next run.</summary>
    public bool SignInRequired { get; init; }

    /// <summary>Why the run stopped early, for the screen; <see langword="null"/> when it ran to the end.</summary>
    public string? StopReason { get; init; }
}
