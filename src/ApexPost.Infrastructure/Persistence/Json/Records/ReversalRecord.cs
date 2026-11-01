namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record ReversalRecord
{
    public required Guid ManagerId { get; init; }

    public required string Reason { get; init; }

    public required DateTimeOffset ReversedAt { get; init; }
}
