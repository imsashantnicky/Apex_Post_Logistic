namespace ApexPost.Application.Abstractions;

/// <summary>The current time, replaceable in tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
