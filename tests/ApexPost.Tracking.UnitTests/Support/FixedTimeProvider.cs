namespace ApexPost.Tracking.UnitTests.Support;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset Default = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    public FixedTimeProvider()
        : this(Default)
    {
    }

    public override DateTimeOffset GetUtcNow() => now;
}
