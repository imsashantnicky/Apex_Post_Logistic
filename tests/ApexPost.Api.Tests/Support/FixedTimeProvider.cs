namespace ApexPost.Api.Tests.Support;

/// <summary>A clock that always shows the same time.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset DefaultNow = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public FixedTimeProvider()
        : this(DefaultNow)
    {
    }

    public override DateTimeOffset GetUtcNow() => now;
}
