namespace ApexPost.Domain.Events;

/// <summary>Something that happened in the domain and that other parts of the system react to.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
