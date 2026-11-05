namespace ApexPost.Courier.Offline.Outbox;

/// <summary>The outcome of one outbox run.</summary>
/// <param name="Attempted">How many scans the run tried to upload.</param>
/// <param name="Synced">How many of them the server accepted; they are now Synced.</param>
/// <param name="Failed">How many of them failed; they are now Failed and are retried later.</param>
public sealed record OutboxResult(int Attempted, int Synced, int Failed)
{
    /// <summary>The result of a run that uploaded nothing, for example because another run was busy.</summary>
    public static OutboxResult Empty { get; } = new(0, 0, 0);
}
